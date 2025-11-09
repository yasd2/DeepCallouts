using LSPD_First_Response.Mod.API;
using LSPD_First_Response.Mod.Callouts;
using Rage;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

public abstract class DeepCalloutBase : Callout
{
    protected static Settings settings;
    protected static Random rand = new Random();
    protected List<Entity> spawnedEntities = new List<Entity>();
    protected List<Blip> spawnedBlips = new List<Blip>();
    protected Ped playerCharacter => Game.LocalPlayer.Character;
    protected LHandle currentPursuit; // Moved from Main to DeepCalloutBase as instance field

    public override bool OnBeforeCalloutDisplayed()
    {
        LoadSettings();
        return settings.EnablePlugin;
    }

    protected void DisplayNotification(string message, bool isError = false)
    {
        if (settings.ShowNotifications)
        {
            Game.DisplayNotification(isError ? "~r~DeepCallouts:~w~ " + message : "~b~DeepCallouts:~w~ " + message);
        }
        if (settings.EnableDebugLogging) Game.LogTrivial($"DeepCallouts LOG: {message}");
    }
    protected string GetRandomText(string baseKey, int count)
    {
        int randomIndex = rand.Next(1, count + 1);
        return LanguageManager.GetText($"{baseKey}_{randomIndex}");
    }

    protected Ped SpawnPed(string modelName, Vector3 position, float heading = 0f, bool isPersistent = true, bool blockPermanentEvents = true)

    {
        if (string.IsNullOrEmpty(modelName)) return null;
        if (position == Vector3.Zero) return null;
        try
        {

            Model model = new Model(modelName);
            model.LoadAndWait();
            if (!model.IsLoaded) { return null; }

            Ped ped = new Ped(model, position, heading);

            if (ped?.Exists() == true)
            {
                ped.IsPersistent = isPersistent;
                ped.BlockPermanentEvents = blockPermanentEvents;
                spawnedEntities.Add(ped);
                return ped;
            }
        }
        catch (Exception e) { Game.LogTrivial($"DeepCallouts ERROR: Failed to spawn ped {modelName}: {e.Message}"); }
        return null;
    }

    protected Vehicle SpawnVehicle(string modelName, Vector3 position, float heading = 0f, bool isPersistent = true)
    {
        if (string.IsNullOrEmpty(modelName)) return null;
        if (position == Vector3.Zero) return null;
        try
        {

            Model model = new Model(modelName);
            model.LoadAndWait();
            if (!model.IsLoaded) { return null; }

            Vehicle vehicle = new Vehicle(model, position, heading);

            if (vehicle?.Exists() == true)
            {
                vehicle.IsPersistent = isPersistent;
                spawnedEntities.Add(vehicle);
                return vehicle;
            }
        }
        catch (Exception e) { Game.LogTrivial($"DeepCallouts ERROR: Failed to spawn vehicle {modelName}: {e.Message}"); }
        return null;
    }

    public static class AICommandSystem
    {
        private static List<Ped> backupUnits = new List<Ped>();
        private static List<Ped> swatUnits = new List<Ped>();
        private static List<Ped> medicalUnits = new List<Ped>();
        private static List<Vehicle> emergencyVehicles = new List<Vehicle>();
        private static bool perimeterSet = false;
        private static Vector3 perimeterCenter = Vector3.Zero;

        public static void ProcessAICommands(DeepCalloutBase callout)
        {
            if (!DeepCalloutBase.GetSettings().EnableEnhancedAI) return;

            var settings = DeepCalloutBase.GetSettings();
            var player = Game.LocalPlayer.Character;

            // Request Backup
            if (Game.IsKeyDown(settings.RequestBackupKey))
            {
                RequestBackup(player.Position, callout);
            }

            // Coordinate SWAT
            if (Game.IsKeyDown(settings.CoordinateSwatKey))
            {
                CoordinateSwatUnits(player.Position, callout);
            }

            // Call Ambulance
            if (Game.IsKeyDown(settings.CallAmbulanceKey))
            {
                CallMedicalSupport(player.Position, callout);
            }

            // Set Perimeter
            if (Game.IsKeyDown(settings.SetPerimeterKey))
            {
                SetPerimeter(player.Position, callout);
            }
        }

        private static void RequestBackup(Vector3 location, DeepCalloutBase callout)
        {
            var settings = DeepCalloutBase.GetSettings();

            if (backupUnits.Count >= settings.MaxBackupUnits)
            {
                callout.DisplayNotification("Maximum backup units already deployed!");
                return;
            }

            callout.DisplayNotification("~b~Requesting backup units...~w~");

            GameFiber.StartNew(() =>
            {
                GameFiber.Sleep(settings.BackupResponseTime * 1000);

                // Spawn backup units
                for (int i = 0; i < 2; i++)
                {
                    Vector3 spawnPos = World.GetNextPositionOnStreet(location.Around(100f, 150f));
                    if (spawnPos != Vector3.Zero)
                    {
                        // Spawn backup vehicle
                        Vehicle backupCar = callout.SpawnVehicle("POLICE", spawnPos, 0f, true);
                        if (backupCar?.Exists() == true)
                        {
                            emergencyVehicles.Add(backupCar);

                            // Spawn backup officers
                            Ped driver = callout.SpawnPed("s_m_y_cop_01", backupCar.Position, 0f, true, true);
                            Ped passenger = callout.SpawnPed("s_f_y_cop_01", backupCar.Position, 0f, true, true);

                            if (driver?.Exists() == true)
                            {
                                driver.WarpIntoVehicle(backupCar, -1);
                                driver.Inventory.GiveNewWeapon(WeaponHash.Pistol, 999, true);
                                backupUnits.Add(driver);
                            }

                            if (passenger?.Exists() == true)
                            {
                                passenger.WarpIntoVehicle(backupCar, 0);
                                passenger.Inventory.GiveNewWeapon(WeaponHash.Pistol, 999, true);
                                backupUnits.Add(passenger);
                            }

                            // Drive to scene
                            if (driver?.Exists() == true)
                            {
                                driver.Tasks.DriveToPosition(location, 25f, VehicleDrivingFlags.Emergency);
                            }
                        }
                    }
                }

                callout.DisplayNotification($"~g~Backup units responding! ETA: 30 seconds~w~");
            });
        }

        private static void CoordinateSwatUnits(Vector3 location, DeepCalloutBase callout)
        {
            if (swatUnits.Count == 0)
            {
                // Spawn SWAT team if none exist
                callout.DisplayNotification("~b~Deploying SWAT team...~w~");

                GameFiber.StartNew(() =>
                {
                    GameFiber.Sleep(60000); // 1 minute response time for SWAT

                    Vector3 swatSpawn = World.GetNextPositionOnStreet(location.Around(80f, 120f));
                    if (swatSpawn != Vector3.Zero)
                    {
                        // Spawn SWAT van
                        Vehicle swatVan = callout.SpawnVehicle("FBI2", swatSpawn, 0f, true);
                        if (swatVan?.Exists() == true)
                        {
                            emergencyVehicles.Add(swatVan);

                            // Spawn SWAT team
                            string[] swatModels = { "s_m_y_swat_01", "s_m_y_blackops_01", "s_m_y_blackops_02" };

                            for (int i = 0; i < 4; i++)
                            {
                                string model = swatModels[rand.Next(swatModels.Length)];
                                Ped swatOfficer = callout.SpawnPed(model, swatVan.Position.Around(3f), 0f, true, true);

                                if (swatOfficer?.Exists() == true)
                                {
                                    swatOfficer.Inventory.GiveNewWeapon(WeaponHash.CarbineRifle, 999, true);
                                    swatOfficer.Armor = 100;
                                    swatUnits.Add(swatOfficer);

                                    // SWAT officers take tactical positions
                                    Vector3 tacticalPos = location.Around(rand.Next(15, 25));
                                    swatOfficer.Tasks.GoStraightToPosition(tacticalPos, 1.5f, 0f, 0f, 10000);
                                }
                            }
                        }
                    }

                    callout.DisplayNotification("~g~SWAT team deployed and taking positions!~w~");
                });
            }
            else
            {
                // Coordinate existing SWAT units
                callout.DisplayNotification("~b~Coordinating SWAT positions...~w~");

                foreach (var swat in swatUnits.Where(s => s.Exists()))
                {
                    Vector3 newPosition = location.Around(rand.Next(10, 20));
                    swat.Tasks.Clear();
                    swat.Tasks.GoStraightToPosition(newPosition, 1.0f, 0f, 0f, 8000);
                }

                callout.DisplayNotification("~g~SWAT units repositioned!~w~");
            }
        }

        private static void CallMedicalSupport(Vector3 location, DeepCalloutBase callout)
        {
            if (medicalUnits.Count > 0)
            {
                callout.DisplayNotification("Medical support already on scene!");
                return;
            }

            callout.DisplayNotification("~b~Calling medical support...~w~");

            GameFiber.StartNew(() =>
            {
                GameFiber.Sleep(30000); // 30 second response time for ambulance

                Vector3 ambulanceSpawn = World.GetNextPositionOnStreet(location.Around(50f, 100f));
                if (ambulanceSpawn != Vector3.Zero)
                {
                    Vehicle ambulance = callout.SpawnVehicle("AMBULAN", ambulanceSpawn, 0f, true);
                    if (ambulance?.Exists() == true)
                    {
                        emergencyVehicles.Add(ambulance);

                        // Spawn paramedics
                        Ped paramedic1 = callout.SpawnPed("s_m_m_paramedic_01", ambulance.Position, 0f, true, true);
                        Ped paramedic2 = callout.SpawnPed("s_m_m_paramedic_01", ambulance.Position, 0f, true, true);

                        if (paramedic1?.Exists() == true)
                        {
                            paramedic1.WarpIntoVehicle(ambulance, -1);
                            medicalUnits.Add(paramedic1);

                            // Drive to scene
                            paramedic1.Tasks.DriveToPosition(location, 20f, VehicleDrivingFlags.Emergency);
                        }

                        if (paramedic2?.Exists() == true)
                        {
                            paramedic2.WarpIntoVehicle(ambulance, 0);
                            medicalUnits.Add(paramedic2);
                        }
                    }
                }

                callout.DisplayNotification("~g~Medical support en route!~w~");
            });
        }

        private static void SetPerimeter(Vector3 location, DeepCalloutBase callout)
        {
            if (perimeterSet)
            {
                callout.DisplayNotification("Perimeter already established!");
                return;
            }

            perimeterSet = true;
            perimeterCenter = location;

            callout.DisplayNotification("~b~Establishing perimeter...~w~");

            // Use existing backup units to set perimeter
            var availableUnits = backupUnits.Where(u => u.Exists()).Take(4).ToList();

            if (availableUnits.Count < 2)
            {
                callout.DisplayNotification("~r~Insufficient units for perimeter! Request backup first.~w~");
                perimeterSet = false;
                return;
            }

            // Position units around the perimeter
            float[] angles = { 0f, 90f, 180f, 270f };

            for (int i = 0; i < Math.Min(availableUnits.Count, 4); i++)
            {
                var unit = availableUnits[i];
                float angle = angles[i];
                Vector3 perimeterPos = location.Around(30f); // 30 meters from center

                unit.Tasks.Clear();
                unit.Tasks.GoStraightToPosition(perimeterPos, 1.0f, angle, 0f, 10000);
            }

            callout.DisplayNotification("~g~Perimeter established! Area secured.~w~");
        }

        public static void CleanupAIUnits()
        {
            // Clean up all AI units when callout ends
            foreach (var unit in backupUnits.Concat(swatUnits).Concat(medicalUnits).Where(u => u.Exists()))
            {
                try
                {
                    unit.Tasks.Clear();
                    unit.Dismiss();
                }
                catch { }
            }

            foreach (var vehicle in emergencyVehicles.Where(v => v.Exists()))
            {
                try
                {
                    vehicle.Delete();
                }
                catch { }
            }

            backupUnits.Clear();
            swatUnits.Clear();
            medicalUnits.Clear();
            emergencyVehicles.Clear();
            perimeterSet = false;
        }

        private static Random rand = new Random();
    }

    protected Blip CreateBlip(Vector3 position, BlipSprite sprite, Color color, string name, bool enableRoute = true)
    {
        try
        {
            if (position == Vector3.Zero) return null;
            Blip blip = new Blip(position);
            if (blip.Exists())
            {
                blip.Sprite = sprite;
                blip.Color = color;
                blip.Name = name;
                blip.Alpha = 0.8f;
                blip.Scale = 0.8f;
                if (enableRoute) blip.EnableRoute(color);
                spawnedBlips.Add(blip);
                return blip;
            }
        }
        catch (Exception e) { Game.LogTrivial($"DeepCallouts ERROR: Failed to create blip: {e.Message}"); }
        return null;
    }
    protected void ProcessEnhancedAI()
    {
        if (settings.EnableEnhancedAI)
        {
            AICommandSystem.ProcessAICommands(this);

            // Show help periodically
            if (Game.GameTime % 30000 < 100) // Every 30 seconds
            {
                DisplayNotification(LanguageManager.GetText("AI_COMMANDS_HELP"));
            }
        }
    }

    public override void End()
    {
        AICommandSystem.CleanupAIUnits();
        base.End();
        CleanupEntities();
    }

    protected void CleanupEntities()
    {
        foreach (var entity in spawnedEntities.Where(e => e?.Exists() == true).ToList())
        {
            try
            {
                if (entity is Ped ped)
                {
                    ped.Tasks.Clear();
                    ped.Dismiss();
                }
                entity.Delete();
            }
            catch { }
        }
        spawnedEntities.Clear();

        foreach (var blip in spawnedBlips.Where(b => b?.Exists() == true).ToList())
        {
            try { blip.Delete(); } catch { }
        }
        spawnedBlips.Clear();
    }

    public static void LoadSettings()
    {
        if (settings != null) return; // Already loaded

        settings = new Settings();

        string pluginDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins", "LSPDFR");
        Directory.CreateDirectory(pluginDir);

        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins", "LSPDFR", "DeepCallouts.ini");

        if (File.Exists(path))
        {
            try
            {
                InitializationFile ini = new InitializationFile(path);

                settings.EnablePlugin = bool.Parse(ini.ReadString("Settings", "EnablePlugin", settings.EnablePlugin.ToString()));
                settings.ShowNotifications = bool.Parse(ini.ReadString("Settings", "ShowNotifications", settings.ShowNotifications.ToString()));
                settings.EnableDebugLogging = bool.Parse(ini.ReadString("Settings", "EnableDebugLogging", settings.EnableDebugLogging.ToString()));
                settings.CalloutCooldownSeconds = int.Parse(ini.ReadString("Settings", "CalloutCooldownSeconds", settings.CalloutCooldownSeconds.ToString())); // Re-added
                settings.Language = ini.ReadString("Settings", "Language", settings.Language);

                // Load keybind settings (still needed for in-callout interactions)
                settings.NegotiationOption1Key = (Keys)Enum.Parse(typeof(Keys), ini.ReadString("Keybinds", "NegotiationOption1Key", settings.NegotiationOption1Key.ToString()));
                settings.NegotiationOption1Modifier = (Keys)Enum.Parse(typeof(Keys), ini.ReadString("Keybinds", "NegotiationOption1Modifier", settings.NegotiationOption1Modifier.ToString()));
                settings.NegotiationOption2Key = (Keys)Enum.Parse(typeof(Keys), ini.ReadString("Keybinds", "NegotiationOption2Key", settings.NegotiationOption2Key.ToString()));
                settings.NegotiationOption2Modifier = (Keys)Enum.Parse(typeof(Keys), ini.ReadString("Keybinds", "NegotiationOption2Modifier", settings.NegotiationOption2Modifier.ToString()));
                settings.YellCommandsKey = (Keys)Enum.Parse(typeof(Keys), ini.ReadString("Keybinds", "YellCommandsKey", settings.YellCommandsKey.ToString()));
                settings.YellCommandsModifier = (Keys)Enum.Parse(typeof(Keys), ini.ReadString("Keybinds", "YellCommandsModifier", settings.YellCommandsModifier.ToString()));
                settings.EvacuateStudentKey = (Keys)Enum.Parse(typeof(Keys), ini.ReadString("Keybinds", "EvacuateStudentKey", settings.EvacuateStudentKey.ToString()));
                settings.EvacuateStudentModifier = (Keys)Enum.Parse(typeof(Keys), ini.ReadString("Keybinds", "EvacuateStudentModifier", settings.EvacuateStudentModifier.ToString()));

                // Load Enhanced AI keybinds
                settings.RequestBackupKey = (Keys)Enum.Parse(typeof(Keys), ini.ReadString("EnhancedAI", "RequestBackupKey", settings.RequestBackupKey.ToString()));
                settings.RequestBackupModifier = (Keys)Enum.Parse(typeof(Keys), ini.ReadString("EnhancedAI", "RequestBackupModifier", settings.RequestBackupModifier.ToString()));
                settings.CoordinateSwatKey = (Keys)Enum.Parse(typeof(Keys), ini.ReadString("EnhancedAI", "CoordinateSwatKey", settings.CoordinateSwatKey.ToString()));
                settings.CoordinateSwatModifier = (Keys)Enum.Parse(typeof(Keys), ini.ReadString("EnhancedAI", "CoordinateSwatModifier", settings.CoordinateSwatModifier.ToString()));
                settings.CallAmbulanceKey = (Keys)Enum.Parse(typeof(Keys), ini.ReadString("EnhancedAI", "CallAmbulanceKey", settings.CallAmbulanceKey.ToString()));
                settings.CallAmbulanceModifier = (Keys)Enum.Parse(typeof(Keys), ini.ReadString("EnhancedAI", "CallAmbulanceModifier", settings.CallAmbulanceModifier.ToString()));
                settings.SetPerimeterKey = (Keys)Enum.Parse(typeof(Keys), ini.ReadString("EnhancedAI", "SetPerimeterKey", settings.SetPerimeterKey.ToString()));
                settings.SetPerimeterModifier = (Keys)Enum.Parse(typeof(Keys), ini.ReadString("EnhancedAI", "SetPerimeterModifier", settings.SetPerimeterModifier.ToString()));

                // Load Enhanced AI settings
                settings.EnableEnhancedAI = bool.Parse(ini.ReadString("EnhancedAI", "EnableEnhancedAI", settings.EnableEnhancedAI.ToString()));
                settings.EnableDynamicBackup = bool.Parse(ini.ReadString("EnhancedAI", "EnableDynamicBackup", settings.EnableDynamicBackup.ToString()));
                settings.MaxBackupUnits = int.Parse(ini.ReadString("EnhancedAI", "MaxBackupUnits", settings.MaxBackupUnits.ToString()));
                settings.BackupResponseTime = int.Parse(ini.ReadString("EnhancedAI", "BackupResponseTime", settings.BackupResponseTime.ToString()));

                // Load callout-specific settings
                settings.HostageNegotiationTimeLimitSeconds = int.Parse(ini.ReadString("HostageSituation", "HostageNegotiationTimeLimitSeconds", settings.HostageNegotiationTimeLimitSeconds.ToString()));
                settings.HostageEscapeCarModel = ini.ReadString("HostageSituation", "HostageEscapeCarModel", settings.HostageEscapeCarModel);
                settings.HostageEscapeCarDistance = float.Parse(ini.ReadString("HostageSituation", "HostageEscapeCarDistance", settings.HostageEscapeCarDistance.ToString()));
                settings.HostageSuspectModel = ini.ReadString("HostageSituation", "HostageSuspectModel", settings.HostageSuspectModel);
                settings.HostageCount = int.Parse(ini.ReadString("HostageSituation", "HostageCount", settings.HostageCount.ToString()));
                settings.HostageModel = ini.ReadString("HostageSituation", "HostageModel", settings.HostageModel);

                settings.AbductionSuspectVehicleModel = ini.ReadString("ChildAbduction", "AbductionSuspectVehicleModel", settings.AbductionSuspectVehicleModel);
                settings.AbductionSuspectModel = ini.ReadString("ChildAbduction", "AbductionSuspectModel", settings.AbductionSuspectModel);
                settings.AbductionChildModel = ini.ReadString("ChildAbduction", "AbductionChildModel", settings.AbductionChildModel);
                settings.AbductionNegotiationTimeLimitSeconds = int.Parse(ini.ReadString("ChildAbduction", "AbductionNegotiationTimeLimitSeconds", settings.AbductionNegotiationTimeLimitSeconds.ToString()));

                settings.SchoolLockdownLocationX = float.Parse(ini.ReadString("SchoolLockdown", "SchoolLockdownLocationX", settings.SchoolLockdownLocationX.ToString()));
                settings.SchoolLockdownLocationY = float.Parse(ini.ReadString("SchoolLockdown", "SchoolLockdownLocationY", settings.SchoolLockdownLocationY.ToString()));
                settings.SchoolLockdownLocationZ = float.Parse(ini.ReadString("SchoolLockdown", "SchoolLockdownLocationZ", settings.SchoolLockdownLocationZ.ToString()));
                settings.SchoolShooterModel = ini.ReadString("SchoolLockdown", "SchoolShooterModel", settings.SchoolShooterModel);
                settings.SchoolSWATCommanderModel = ini.ReadString("SchoolLockdown", "SchoolSWATCommanderModel", settings.SchoolSWATCommanderModel);
                settings.SchoolStudentModel = ini.ReadString("SchoolLockdown", "SchoolStudentModel", settings.SchoolStudentModel);
                settings.SchoolFacultyModel = ini.ReadString("SchoolLockdown", "SchoolFacultyModel", settings.SchoolFacultyModel);
                settings.SchoolBreachDelaySeconds = int.Parse(ini.ReadString("SchoolLockdown", "SchoolBreachDelaySeconds", settings.SchoolBreachDelaySeconds.ToString()));

                // Bank Robbery settings
                settings.BankRobberyLocationX = float.Parse(ini.ReadString("BankRobbery", "BankRobberyLocationX", settings.BankRobberyLocationX.ToString()));
                settings.BankRobberyLocationY = float.Parse(ini.ReadString("BankRobbery", "BankRobberyLocationY", settings.BankRobberyLocationY.ToString()));
                settings.BankRobberyLocationZ = float.Parse(ini.ReadString("BankRobbery", "BankRobberyLocationZ", settings.BankRobberyLocationZ.ToString()));
                settings.BankRobberModel = ini.ReadString("BankRobbery", "BankRobberModel", settings.BankRobberModel);
                settings.BankHostageModel = ini.ReadString("BankRobbery", "BankHostageModel", settings.BankHostageModel);
                settings.BankRobberCount = int.Parse(ini.ReadString("BankRobbery", "BankRobberCount", settings.BankRobberCount.ToString()));

                // Home Invasion settings
                settings.HomeInvaderModel = ini.ReadString("HomeInvasion", "HomeInvaderModel", settings.HomeInvaderModel);
                settings.HomeOwnerModel = ini.ReadString("HomeInvasion", "HomeOwnerModel", settings.HomeOwnerModel);
                settings.HomeInvaderCount = int.Parse(ini.ReadString("HomeInvasion", "HomeInvaderCount", settings.HomeInvaderCount.ToString()));

                // Suicide Situation settings
                settings.SuicideVictimModel = ini.ReadString("SuicideSituation", "SuicideVictimModel", settings.SuicideVictimModel);
                settings.SuicideNegotiationTimeLimit = int.Parse(ini.ReadString("SuicideSituation", "SuicideNegotiationTimeLimit", settings.SuicideNegotiationTimeLimit.ToString()));
                settings.SuicideCounselorModel = ini.ReadString("SuicideSituation", "SuicideCounselorModel", settings.SuicideCounselorModel);

                // Validate settings (only relevant ones)
                settings.HostageNegotiationTimeLimitSeconds = Math.Max(30, settings.HostageNegotiationTimeLimitSeconds);
                settings.HostageEscapeCarDistance = Math.Max(10f, settings.HostageEscapeCarDistance);
                settings.HostageCount = Math.Max(1, Math.Min(5, settings.HostageCount));
                settings.AbductionNegotiationTimeLimitSeconds = Math.Max(0, settings.AbductionNegotiationTimeLimitSeconds);
                settings.SchoolBreachDelaySeconds = Math.Max(0, settings.SchoolBreachDelaySeconds);
                settings.BankRobberCount = Math.Max(1, Math.Min(4, settings.BankRobberCount));
                settings.HomeInvaderCount = Math.Max(1, Math.Min(3, settings.HomeInvaderCount));
                settings.SuicideNegotiationTimeLimit = Math.Max(60, settings.SuicideNegotiationTimeLimit);
                settings.MaxBackupUnits = Math.Max(2, Math.Min(10, settings.MaxBackupUnits));
                settings.BackupResponseTime = Math.Max(15, Math.Min(120, settings.BackupResponseTime));

                Game.LogTrivial("DeepCallouts settings loaded successfully.");
            }
            catch (Exception e)
            {
                Game.LogTrivial("Error loading DeepCallouts settings: " + e.Message);
                settings = new Settings();
            }
        }
        else
        {
            // Create default settings file
            CreateDefaultSettings();
        }
    }

    // access settings from Main class
    public static Settings GetSettings()
    {
        if (settings == null) LoadSettings();
        return settings;
    }

    private static void CreateDefaultSettings()
    {
        try
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins", "LSPDFR", "DeepCallouts.ini");
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            using (StreamWriter sw = File.CreateText(path))
            {
                sw.WriteLine("[Settings]");
                sw.WriteLine("EnablePlugin = true");
                sw.WriteLine("ShowNotifications = true");
                sw.WriteLine("EnableDebugLogging = false");
                sw.WriteLine("CalloutCooldownSeconds = 30"); // Re-added
                sw.WriteLine("Language = auto");
                sw.WriteLine();
                sw.WriteLine("[Keybinds]");
                sw.WriteLine("NegotiationOption1Key = NumPad1");
                sw.WriteLine("NegotiationOption1Modifier = None");
                sw.WriteLine("NegotiationOption2Key = NumPad2");
                sw.WriteLine("NegotiationOption2Modifier = None");
                sw.WriteLine("YellCommandsKey = Y");
                sw.WriteLine("YellCommandsModifier = None");
                sw.WriteLine("EvacuateStudentKey = B");
                sw.WriteLine("EvacuateStudentModifier = None");
                sw.WriteLine();
                sw.WriteLine("[HostageSituation]");
                sw.WriteLine("HostageNegotiationTimeLimitSeconds = 180");
                sw.WriteLine("HostageEscapeCarModel = FUGITIVE");
                sw.WriteLine("HostageEscapeCarDistance = 50.0");
                sw.WriteLine("HostageSuspectModel = A_M_Y_VINDOUCHE_01");
                sw.WriteLine("HostageCount = 1");
                sw.WriteLine("HostageModel = A_F_Y_BUSINESS_02");
                sw.WriteLine();
                sw.WriteLine("[ChildAbduction]");
                sw.WriteLine("AbductionSuspectVehicleModel = SENTINEL");
                sw.WriteLine("AbductionSuspectModel = A_M_M_PROLHOST_01");
                sw.WriteLine("AbductionChildModel = A_F_Y_HIPSTER_01");
                sw.WriteLine("AbductionNegotiationTimeLimitSeconds = 120");
                sw.WriteLine();
                sw.WriteLine("[SchoolLockdown]");
                sw.WriteLine("SchoolLockdownLocationX = -1696.866");
                sw.WriteLine("SchoolLockdownLocationY = 142.747");
                sw.WriteLine("SchoolLockdownLocationZ = 64.372");
                sw.WriteLine("SchoolShooterModel = A_M_Y_VINDOUCHE_01");
                sw.WriteLine("SchoolSWATCommanderModel = S_M_Y_SWAT_01");
                sw.WriteLine("SchoolStudentModel = A_F_Y_HIPSTER_02");
                sw.WriteLine("SchoolFacultyModel = A_M_M_BUSINESS_01");
                sw.WriteLine("SchoolBreachDelaySeconds = 10");
                sw.WriteLine();
                sw.WriteLine("[BankRobbery]");
                sw.WriteLine("BankRobberyLocationX = 75.38659");
                sw.WriteLine("BankRobberyLocationY = -818.9402");
                sw.WriteLine("BankRobberyLocationZ = 44.57247");
                sw.WriteLine("BankRobberModel = A_M_M_PROLHOST_01");
                sw.WriteLine("BankHostageModel = A_F_Y_BUSINESS_02");
                sw.WriteLine("BankRobberCount = 2");
                sw.WriteLine();
                sw.WriteLine("[HomeInvasion]");
                sw.WriteLine("HomeInvaderModel = A_M_Y_VINDOUCHE_01");
                sw.WriteLine("HomeOwnerModel = A_M_M_BUSINESS_01");
                sw.WriteLine("HomeInvaderCount = 2");
                sw.WriteLine();
                sw.WriteLine("[SuicideSituation]");
                sw.WriteLine("SuicideVictimModel = A_M_M_BUSINESS_01");
                sw.WriteLine("SuicideNegotiationTimeLimit = 300");
                sw.WriteLine("SuicideCounselorModel = A_F_Y_BUSINESS_02");
                sw.WriteLine();
                sw.WriteLine("[EnhancedAI]");
                sw.WriteLine("RequestBackupKey = R");
                sw.WriteLine("RequestBackupModifier = None");
                sw.WriteLine("CoordinateSwatKey = T");
                sw.WriteLine("CoordinateSwatModifier = None");
                sw.WriteLine("CallAmbulanceKey = M");
                sw.WriteLine("CallAmbulanceModifier = None");
                sw.WriteLine("SetPerimeterKey = P");
                sw.WriteLine("SetPerimeterModifier = None");
                sw.WriteLine("EnableEnhancedAI = true");
                sw.WriteLine("EnableDynamicBackup = true");
                sw.WriteLine("MaxBackupUnits = 6");
                sw.WriteLine("BackupResponseTime = 45");
            }
            Game.LogTrivial("DeepCallouts default settings created.");
        }
        catch (Exception e)
        {
            Game.LogTrivial("Error creating DeepCallouts settings file: " + e.Message);
        }
    }
}