using LSPD_First_Response.Mod.API;
using LSPD_First_Response.Mod.Callouts;
using Rage;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

[CalloutInfo("HomeInvasion", CalloutProbability.Medium)]
public class HomeInvasionCallout : DeepCalloutBase
{
    private Vector3 houseLocation;
    private List<Ped> invaders = new List<Ped>();
    private List<Ped> homeOwners = new List<Ped>();
    private List<Vehicle> invaderVehicles = new List<Vehicle>();
    private bool invasionActive = false;
    private bool invadersEscaping = false;
    private int difficultyLevel = 1; // 1 = Easy, 2 = Medium, 3 = Hard

    public override bool OnBeforeCalloutDisplayed()
    {
        if (!base.OnBeforeCalloutDisplayed()) return false;

        // Find a random house location
        houseLocation = playerCharacter.Position.Around(200f, 500f);
        houseLocation = World.GetNextPositionOnStreet(houseLocation);

        if (houseLocation == Vector3.Zero) return false;

        // Dynamic difficulty based on player equipment/location
        CalculateDifficultyLevel();

        // Set callout properties
        this.CalloutMessage = LanguageManager.GetText("HOME_INVASION");
        this.CalloutPosition = houseLocation;
        this.CalloutAdvisory = LanguageManager.GetText("HOME_INVASION_DESC");

        Functions.PlayScannerAudioUsingPosition("ATTENTION_ALL_UNITS WE_HAVE CRIME_BURGLARY_IN_PROGRESS IN_OR_ON_POSITION", houseLocation);

        return true;
    }

    public override bool OnCalloutAccepted()
    {
        CreateBlip(houseLocation, BlipSprite.Safehouse, Color.Red, LanguageManager.GetText("HOME_INVASION"));
        DisplayNotification(LanguageManager.GetText("HOME_INVASION_RESPOND"));
        return base.OnCalloutAccepted();
    }

    public override void Process()
    {
        base.Process();

        // Start scenario when player gets close
        if (playerCharacter.DistanceTo(houseLocation) < 80f && !invasionActive)
        {
            StartHomeInvasionScenario();
        }

        // Handle escape phase
        if (invadersEscaping)
        {
            HandleEscapePhase();
        }

        // Check completion
        CheckCompletionConditions();
    }

    private void CalculateDifficultyLevel()
    {
        // Dynamic difficulty based on player's current weapon and location
        difficultyLevel = 1; // Start with easy

        // Check player's weapon
        if (playerCharacter.Inventory.Weapons.Contains(WeaponHash.CarbineRifle) ||
playerCharacter.Inventory.Weapons.Contains(WeaponHash.AssaultRifle))
        {
            difficultyLevel = Math.Min(3, difficultyLevel + 1);
        }

        // Check if in wealthy area (simplified check based on coordinates)
        if (houseLocation.X > -500f && houseLocation.Y > -500f)
        {
            difficultyLevel = Math.Min(3, difficultyLevel + 1);
        }

        // Random factor
        if (rand.Next(0, 3) == 0)
        {
            difficultyLevel = Math.Min(3, difficultyLevel + 1);
        }
    }

    private void StartHomeInvasionScenario()
    {
        invasionActive = true;

        // Adjust invader count based on difficulty
        int invaderCount = Math.Min(settings.HomeInvaderCount + (difficultyLevel - 1), 4);

        // Spawn invader vehicles
        for (int i = 0; i < Math.Max(1, invaderCount / 2); i++)
        {
            Vector3 vehiclePos = World.GetNextPositionOnStreet(houseLocation.Around(30f, 50f));
            if (vehiclePos != Vector3.Zero)
            {
                string[] vehicleModels = { "BALLER", "DUBSTA", "CAVALCADE", "GRANGER" };
                Vehicle invaderVehicle = SpawnVehicle(vehicleModels[rand.Next(vehicleModels.Length)], vehiclePos, 0f, true);
                if (invaderVehicle?.Exists() == true)
                {
                    invaderVehicles.Add(invaderVehicle);
                    CreateBlip(invaderVehicle.Position, BlipSprite.GetawayCar, Color.Orange, "Invader Vehicle");
                }
            }
        }

        // Spawn invaders with difficulty-based equipment
        string[] invaderModels = { "A_M_Y_VINDOUCHE_01", "A_M_M_PROLHOST_01", "A_M_Y_HIPSTER_01", "A_M_M_SKATER_01" };
        WeaponHash[] weapons = GetWeaponsForDifficulty();

        for (int i = 0; i < invaderCount; i++)
        {
            Vector3 invaderPos = houseLocation.Around(rand.Next(5, 15));
            string model = invaderModels[rand.Next(invaderModels.Length)];
            Ped invader = SpawnPed(model, invaderPos, 0f, true, true);

            if (invader != null)
            {
                invaders.Add(invader);
                CreateBlip(invader.Position, BlipSprite.Enemy, Color.Red, LanguageManager.GetText("HOME_INVADER") + " " + (i + 1));

                // Give weapon based on difficulty
                WeaponHash weapon = weapons[rand.Next(weapons.Length)];
                invader.Inventory.GiveNewWeapon(weapon, 999, true);
                invader.Accuracy = 40 + (difficultyLevel * 15); // 55, 70, 85 accuracy

                // Set behavior based on difficulty
                if (difficultyLevel >= 2)
                {
                    invader.Tasks.Wander(); // More active on higher difficulty
                }
                else
                {
                    invader.Tasks.StandStill(-1); // Static on easy
                }
            }
        }

        // Spawn home owners (victims)
        string[] ownerModels = { "A_M_M_BUSINESS_01", "A_F_M_BUSINESS_02", "A_M_Y_BUSINESS_01", "A_F_Y_BUSINESS_02" };
        int ownerCount = rand.Next(1, 3);

        for (int i = 0; i < ownerCount; i++)
        {
            Vector3 ownerPos = houseLocation.Around(rand.Next(8, 20));
            string model = ownerModels[rand.Next(ownerModels.Length)];
            Ped owner = SpawnPed(model, ownerPos, 0f, true, true);

            if (owner != null)
            {
                homeOwners.Add(owner);
                CreateBlip(owner.Position, BlipSprite.Friend, Color.Green, LanguageManager.GetText("HOME_OWNER") + " " + (i + 1));

                // Owners are scared and hiding
                owner.Tasks.ReactAndFlee(invaders.FirstOrDefault());
            }
        }

        DisplayNotification($"Home invasion in progress! Difficulty: {GetDifficultyText()}");
        DisplayNotification("Approach carefully - suspects may be armed and dangerous!");

        // Start timer for invaders to potentially escape
        GameFiber.StartNew(() =>
        {
            int timeBeforeEscape = 60000 + (difficultyLevel * 30000); // 60s, 90s, 120s
            GameFiber.Sleep(timeBeforeEscape);

            if (invasionActive && invaders.Any(i => i.Exists() && i.IsAlive))
            {
                DisplayNotification("Invaders are preparing to flee! Move in now!");
                GameFiber.Sleep(15000); // 15 second warning

                if (invaders.Any(i => i.Exists() && i.IsAlive))
                {
                    StartEscapeSequence();
                }
            }
        });
    }

    private WeaponHash[] GetWeaponsForDifficulty()
    {
        switch (difficultyLevel)
        {
            case 1: // Easy
                return new[] { WeaponHash.Pistol, WeaponHash.CombatPistol, WeaponHash.Knife };
            case 2: // Medium
                return new[] { WeaponHash.Pistol, WeaponHash.MicroSMG, WeaponHash.SawnOffShotgun };
            case 3: // Hard
                return new[] { WeaponHash.AssaultRifle, WeaponHash.CarbineRifle, WeaponHash.MicroSMG };
            default:
                return new[] { WeaponHash.Pistol };
        }
    }

    private string GetDifficultyText()
    {
        switch (difficultyLevel)
        {
            case 1: return "Low";
            case 2: return "Medium";
            case 3: return "High";
            default: return "Unknown";
        }
    }

    private void StartEscapeSequence()
    {
        invadersEscaping = true;
        DisplayNotification("Invaders are fleeing the scene!");

        GameFiber.StartNew(() =>
        {
            // Move invaders to their vehicles
            var availableVehicles = invaderVehicles.Where(v => v.Exists()).ToList();

            for (int i = 0; i < invaders.Count && i < availableVehicles.Count * 4; i++)
            {
                var invader = invaders[i];
                if (invader?.Exists() == true && invader.IsAlive)
                {
                    var targetVehicle = availableVehicles[i / 4]; // 4 invaders per vehicle max
                    invader.Tasks.Clear();
                    invader.Tasks.EnterVehicle(targetVehicle, i % 4 - 1); // -1 = driver, 0-2 = passengers
                }
            }

            GameFiber.Sleep(8000); // Give them time to get in vehicles

            // Start pursuit with vehicles that have invaders
            foreach (var vehicle in availableVehicles)
            {
                if (vehicle.Exists() && vehicle.HasOccupants)
                {
                    var driver = vehicle.Driver;
                    if (driver?.Exists() == true)
                    {
                        driver.Tasks.CruiseWithVehicle(vehicle, 40f, VehicleDrivingFlags.Emergency);

                        if (currentPursuit == null)
                        {
                            currentPursuit = Functions.CreatePursuit();
                            Functions.SetPursuitIsActiveForPlayer(currentPursuit, true);
                        }
                        Functions.AddPedToPursuit(currentPursuit, driver);
                    }
                }
            }

            if (currentPursuit != null)
            {
                DisplayNotification("Pursuit initiated! Don't let them escape!");
            }
        });
    }

    private void HandleEscapePhase()
    {
        // Monitor pursuit
        if (currentPursuit != null && !Functions.IsPursuitStillRunning(currentPursuit))
        {
            invadersEscaping = false;
            DisplayNotification("Pursuit ended. Check on the home owners.");
        }
    }

    private void CheckCompletionConditions()
    {
        bool invadersNeutralized = invaders.All(i => !i.Exists() || i.IsDead || Functions.IsPedArrested(i));
        bool ownersAlive = homeOwners.All(o => !o.Exists() || o.IsAlive);

        if (invadersNeutralized && ownersAlive)
        {
            DisplayNotification(LanguageManager.GetText("HOME_SECURED"));
            this.End();
        }
        else if (invadersNeutralized && !ownersAlive)
        {
            DisplayNotification(LanguageManager.GetText("CALLOUT_FAILED", "Home Invasion"));
            this.End();
        }
    }

    public override void End()
    {
        if (currentPursuit != null && Functions.IsPursuitStillRunning(currentPursuit))
        {
            Functions.ForceEndPursuit(currentPursuit);
        }
        base.End();
    }
}