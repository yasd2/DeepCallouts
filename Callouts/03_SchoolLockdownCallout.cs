using LSPD_First_Response.Mod.API;
using LSPD_First_Response.Mod.Callouts;
using Rage;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

[CalloutInfo("SchoolLockdown", CalloutProbability.Low)]
public class SchoolLockdownCallout : DeepCalloutBase
{
    private Vector3 schoolLocation;
    private Ped commander;
    private List<Ped> shooters = new List<Ped>();
    private List<Ped> students = new List<Ped>();
    private List<Ped> faculty = new List<Ped>();
    private List<Ped> swatUnits = new List<Ped>();
    private bool breachPhase = false;
    private int civiliansEvacuated = 0;
    public override bool OnBeforeCalloutDisplayed()
    {
        if (!base.OnBeforeCalloutDisplayed()) return false;

        // Set school location
        schoolLocation = new Vector3(settings.SchoolLockdownLocationX, settings.SchoolLockdownLocationY, settings.SchoolLockdownLocationZ);

        // Set callout properties
        this.CalloutMessage = LanguageManager.GetText("SCHOOL_LOCKDOWN");
        this.CalloutPosition = schoolLocation;
        this.CalloutAdvisory = LanguageManager.GetText("LOCKDOWN_DESC");

        Functions.PlayScannerAudioUsingPosition("ATTENTION_ALL_UNITS WE_HAVE CRIME_SCHOOL_SHOOTING IN_OR_ON_POSITION", schoolLocation);

        return true;
    }

    public override bool OnCalloutAccepted()
    {
        // Create initial blip
        CreateBlip(schoolLocation, BlipSprite.Safehouse, Color.Blue, LanguageManager.GetText("SCHOOL_LOCKDOWN"));

        DisplayNotification(LanguageManager.GetText("LOCKDOWN_RESPOND"));

        return base.OnCalloutAccepted();
    }

    public override void Process()
    {
        base.Process();

        // Check if player is close enough to start the scenario
        if (playerCharacter.DistanceTo(schoolLocation) < 100f && commander == null)
        {
            StartLockdownScenario();
        }

        // Handle breach phase
        if (breachPhase)
        {
            HandleBreachPhase();
        }

        // Check completion
        bool shootersNeutralized = shooters.All(s => !s.Exists() || s.IsDead);
        int totalCivilians = students.Count + faculty.Count;

        if (shootersNeutralized && civiliansEvacuated >= totalCivilians)
        {
            DisplayNotification(LanguageManager.GetText("LOCKDOWN_SUCCESS"));
            this.End();
        }
        else if (shootersNeutralized && civiliansEvacuated < totalCivilians)
        {
            // Some civilians were not evacuated, but shooters are neutralized
            DisplayNotification(LanguageManager.GetText("CALLOUT_FAILED", "School Lockdown"));
            this.End();
        }
    }

    private void StartLockdownScenario()
    {
        // Spawn SWAT Commander
        commander = SpawnPed(settings.SchoolSWATCommanderModel, schoolLocation.Around(20f), 0f, true, true);
        if (commander == null) { DisplayNotification("Failed to spawn Commander!", true); this.End(); return; }

        CreateBlip(commander.Position, BlipSprite.Waypoint, Color.Blue, LanguageManager.GetText("SWAT_COMMANDER"));
        commander.Tasks.AchieveHeading(playerCharacter.Heading + 180f, 1000);
        commander.Tasks.StandStill(-1);

        // Spawn SWAT units
        for (int i = 0; i < 3; i++)
        {
            Ped swat = SpawnPed("s_m_y_swat_01", schoolLocation.Around(30f), 0f, true, true);
            if (swat != null)
            {
                swatUnits.Add(swat);
                swat.Tasks.StandStill(-1);
            }
        }

        DisplayNotification(LanguageManager.GetText("TALK_TO_COMMANDER"));

        // Wait for player to approach commander
        GameFiber.StartNew(() =>
        {
            while (playerCharacter.DistanceTo(commander) > 5f && commander.Exists())
                GameFiber.Yield();

            if (commander.Exists())
            {
                StartBriefing();
            }
        });
    }

    private void StartBriefing()
    {
        DisplayNotification(LanguageManager.GetText("COMMANDER_BRIEFING"));
        GameFiber.Sleep(3000);
        DisplayNotification(LanguageManager.GetText("CONFIRM_BREACH", $"Press [E] to confirm breach plan. Entry in {settings.SchoolBreachDelaySeconds} seconds."));

        // Wait for player confirmation
        GameFiber.StartNew(() =>
        {
            while (!Game.IsKeyDownRightNow(Keys.E) && commander.Exists())
                GameFiber.Yield();

            if (commander.Exists())
            {
                InitiateBreach();
            }
        });
    }

    private void InitiateBreach()
    {
        DisplayNotification(LanguageManager.GetText("BREACH_COUNTDOWN"));
        GameFiber.Sleep(settings.SchoolBreachDelaySeconds * 1000);

        SpawnThreats();
        SpawnCivilians();

        breachPhase = true;
        DisplayNotification(LanguageManager.GetText("BREACH_ACTIVE"));
    }

    private void SpawnThreats()
    {
        string[] shooterModels = {
            "A_M_Y_VINDOUCHE_01", "A_M_M_PROLHOST_01", "A_M_Y_HIPSTER_01",
            "A_M_M_SKATER_01", "A_M_Y_SKATER_01", "A_M_M_HILLBILLY_02"
        };

        int shooterCount = rand.Next(1, 4); // 1-3 shooters
        DisplayNotification(LanguageManager.GetText("SHOOTERS_DETECTED", $"ALERT: {shooterCount} shooter{(shooterCount > 1 ? "s" : "")} detected!"));

        for (int i = 0; i < shooterCount; i++)
        {
            Vector3 shooterSpawn = schoolLocation.Around(rand.Next(10, 25));
            shooterSpawn = World.GetNextPositionOnStreet(shooterSpawn);

            string randomModel = shooterModels[rand.Next(shooterModels.Length)];
            Ped shooter = SpawnPed(randomModel, shooterSpawn, 0f, true, true);

            if (shooter != null)
            {
                shooters.Add(shooter);
                CreateBlip(shooter.Position, BlipSprite.Enemy, Color.Red, LanguageManager.GetText("SHOOTER", $"Shooter {i + 1}"));
                shooter.Inventory.GiveNewWeapon(WeaponHash.AssaultRifle, 999, true);
                shooter.Accuracy = rand.Next(60, 90);
                shooter.Tasks.Wander();
            }
        }
    }

    private void SpawnCivilians()
    {
        string[] studentModels = {
            "A_F_Y_HIPSTER_02", "A_M_Y_HIPSTER_01", "A_F_Y_TOURIST_02",
            "A_M_Y_SKATER_01", "A_F_Y_BUSINESS_01", "A_M_Y_BUSINESS_01"
        };

        string[] facultyModels = {
            "A_M_M_BUSINESS_01", "A_F_M_BUSINESS_02", "A_M_M_PROLHOST_01",
            "A_F_M_TOURIST_01", "A_M_M_TOURIST_01", "A_F_M_PROLHOST_01"
        };

        // Spawn students and faculty
        for (int i = 0; i < 5; i++)
        {
            Ped student = SpawnPed(studentModels[rand.Next(studentModels.Length)], schoolLocation.Around(15f), 0f, true, true);
            if (student != null)
            {
                students.Add(student);
                student.Tasks.ReactAndFlee(shooters.FirstOrDefault());
            }

            Ped facultyMember = SpawnPed(facultyModels[rand.Next(facultyModels.Length)], schoolLocation.Around(15f), 0f, true, true);
            if (facultyMember != null)
            {
                faculty.Add(facultyMember);
                facultyMember.Tasks.ReactAndFlee(shooters.FirstOrDefault());
            }
        }
    }

    private void HandleBreachPhase()
    {
        // Handle civilian evacuation
        foreach (var civ in students.Concat(faculty).ToList())
        {
            if (civ.Exists() && civ.IsAlive && playerCharacter.DistanceTo(civ) < 3f)
            {
                if (Game.IsKeyDown(settings.EvacuateStudentKey))
                {
                    civ.Tasks.Clear();
                    civ.Tasks.ReactAndFlee(shooters.FirstOrDefault());
                    civiliansEvacuated++;
                    students.Remove(civ);
                    faculty.Remove(civ);
                    DisplayNotification(LanguageManager.GetText("CIVILIAN_EVACUATED", $"Civilian evacuated! Total: {civiliansEvacuated}"));
                }
            }
        }

        // Handle shooter confrontation
        foreach (var shooter in shooters.Where(s => s.Exists() && s.IsAlive).ToList())
        {
            if (playerCharacter.DistanceTo(shooter) < 5f)
            {
                DisplayNotification(LanguageManager.GetText("SHOOTER_COMMANDS"));
                if (Game.IsKeyDown(settings.YellCommandsKey))
                {
                    bool surrenders = rand.Next(0, 3) == 0; // 33% chance
                    if (surrenders)
                    {
                        DisplayNotification(LanguageManager.GetText("SHOOTER_SURRENDERS"));
                        shooter.Tasks.Clear();
                        shooter.Tasks.PutHandsUp(99999, playerCharacter);
                    }
                    else
                    {
                        DisplayNotification(LanguageManager.GetText("SHOOTER_HOSTILE"));
                        shooter.Tasks.FightAgainst(playerCharacter);
                    }
                }
                break;
            }
        }
    }

    public override void End()
    {
        base.End();
    }
}