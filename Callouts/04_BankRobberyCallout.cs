using LSPD_First_Response.Mod.API;
using LSPD_First_Response.Mod.Callouts;
using Rage;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

[CalloutInfo("BankRobbery", CalloutProbability.Medium)]
public class BankRobberyCallout : DeepCalloutBase
{
    private Vector3 bankLocation;
    private List<Ped> robbers = new List<Ped>();
    private List<Ped> hostages = new List<Ped>();
    private List<Ped> swatUnits = new List<Ped>();
    private Ped swatCommander;
    private Vehicle getawayVehicle;
    private bool negotiationPhase = false;
    private bool robbersEscaping = false;
    private int hostagesToSave;

    public override bool OnBeforeCalloutDisplayed()
    {
        if (!base.OnBeforeCalloutDisplayed()) return false;

        // Set bank location from settings
        bankLocation = new Vector3(settings.BankRobberyLocationX, settings.BankRobberyLocationY, settings.BankRobberyLocationZ);

        // Set callout properties
        this.CalloutMessage = LanguageManager.GetText("BANK_ROBBERY");
        this.CalloutPosition = bankLocation;
        this.CalloutAdvisory = LanguageManager.GetText("BANK_ROBBERY_DESC");

        Functions.PlayScannerAudioUsingPosition("ATTENTION_ALL_UNITS WE_HAVE CRIME_BANK_ROBBERY IN_OR_ON_POSITION", bankLocation);

        return true;
    }

    public override bool OnCalloutAccepted()
    {
        // Create initial blip
        CreateBlip(bankLocation, BlipSprite.TheJewelStoreJob, Color.Red, LanguageManager.GetText("BANK_ROBBERY"));
        DisplayNotification(LanguageManager.GetText("BANK_ROBBERY_RESPOND"));
        return base.OnCalloutAccepted();
    }

    public override void Process()
    {
        base.Process();

        // Check if player is close enough to start the scenario
        if (playerCharacter.DistanceTo(bankLocation) < 100f && robbers.Count == 0)
        {
            StartBankRobberyScenario();
        }

        // Handle negotiation phase
        if (negotiationPhase)
        {
            HandleNegotiation();
        }

        // Handle escape phase
        if (robbersEscaping)
        {
            HandleEscapePhase();
        }

        // Check completion conditions
        CheckCompletionConditions();
    }

    private void StartBankRobberyScenario()
    {
        // Spawn SWAT Commander
        Vector3 commanderPos = bankLocation.Around(50f);
        swatCommander = SpawnPed(settings.SchoolSWATCommanderModel, commanderPos, 0f, true, true);
        if (swatCommander == null) { DisplayNotification("Failed to spawn SWAT Commander!", true); this.End(); return; }

        CreateBlip(swatCommander.Position, BlipSprite.Waypoint, Color.Blue, "SWAT Commander");
        swatCommander.Tasks.StandStill(-1);

        // Spawn SWAT units
        for (int i = 0; i < 4; i++)
        {
            Ped swat = SpawnPed("s_m_y_swat_01", bankLocation.Around(60f), 0f, true, true);
            if (swat != null)
            {
                swatUnits.Add(swat);
                swat.Inventory.GiveNewWeapon(WeaponHash.CarbineRifle, 999, true);
                swat.Tasks.StandStill(-1);
            }
        }

        // Spawn robbers inside bank
        string[] robberModels = { "A_M_M_PROLHOST_01", "A_M_Y_VINDOUCHE_01", "A_M_M_SKATER_01" };
        int robberCount = Math.Min(settings.BankRobberCount, 4);

        for (int i = 0; i < robberCount; i++)
        {
            Vector3 robberPos = bankLocation.Around(rand.Next(5, 15));
            string model = robberModels[rand.Next(robberModels.Length)];
            Ped robber = SpawnPed(model, robberPos, 0f, true, true);

            if (robber != null)
            {
                robbers.Add(robber);
                CreateBlip(robber.Position, BlipSprite.Enemy, Color.Red, LanguageManager.GetText("BANK_ROBBER") + " " + (i + 1));
                robber.Inventory.GiveNewWeapon(WeaponHash.AssaultRifle, 999, true);
                robber.Accuracy = rand.Next(70, 90);
                robber.Tasks.StandStill(-1);
            }
        }

        // Spawn hostages
        string[] hostageModels = { "A_F_Y_BUSINESS_02", "A_M_M_BUSINESS_01", "A_F_M_BUSINESS_02", "A_M_Y_BUSINESS_01" };
        hostagesToSave = rand.Next(3, 6);

        for (int i = 0; i < hostagesToSave; i++)
        {
            Vector3 hostagePos = bankLocation.Around(rand.Next(8, 20));
            string model = hostageModels[rand.Next(hostageModels.Length)];
            Ped hostage = SpawnPed(model, hostagePos, 0f, true, true);

            if (hostage != null)
            {
                hostages.Add(hostage);
                CreateBlip(hostage.Position, BlipSprite.Friend, Color.Yellow, "Hostage " + (i + 1));
                hostage.Tasks.PutHandsUp(99999, robbers.FirstOrDefault());
            }
        }

        // Spawn getaway vehicle
        Vector3 getawayPos = World.GetNextPositionOnStreet(bankLocation.Around(30f));
        if (getawayPos != Vector3.Zero)
        {
            getawayVehicle = SpawnVehicle("KURUMA2", getawayPos, 0f, true);
            if (getawayVehicle?.Exists() == true)
            {
                CreateBlip(getawayVehicle.Position, BlipSprite.GetawayCar, Color.Orange, "Getaway Vehicle");
            }
        }

        DisplayNotification("Bank robbery in progress! Talk to SWAT Commander for briefing.");

        // Wait for player to approach commander
        GameFiber.StartNew(() =>
        {
            while (playerCharacter.DistanceTo(swatCommander) > 5f && swatCommander.Exists())
                GameFiber.Yield();

            if (swatCommander.Exists())
            {
                StartBriefingPhase();
            }
        });
    }

    private void StartBriefingPhase()
    {
        DisplayNotification("Commander: Multiple armed suspects inside with hostages. We need a tactical approach.");
        GameFiber.Sleep(3000);
        DisplayNotification("Press [E] to initiate negotiation or [Q] to authorize tactical breach.");

        GameFiber.StartNew(() =>
        {
            while (swatCommander.Exists() && !negotiationPhase && !robbersEscaping)
            {
                if (Game.IsKeyDownRightNow(Keys.E))
                {
                    StartNegotiationPhase();
                    break;
                }
                else if (Game.IsKeyDownRightNow(Keys.Q))
                {
                    StartTacticalBreach();
                    break;
                }
                GameFiber.Yield();
            }
        });
    }

    private void StartNegotiationPhase()
    {
        negotiationPhase = true;
        DisplayNotification("Negotiation phase initiated. [Numpad1] Demand surrender | [Numpad2] Offer safe passage");

        // Robbers get nervous after some time
        GameFiber.StartNew(() =>
        {
            GameFiber.Sleep(45000); // 45 seconds
            if (negotiationPhase && robbers.Any(r => r.Exists()))
            {
                DisplayNotification("Robbers are getting agitated! They're preparing to leave!");
                StartEscapeSequence();
            }
        });
    }

    private void StartTacticalBreach()
    {
        DisplayNotification("Tactical breach authorized! SWAT moving in!");

        foreach (var swat in swatUnits.Where(s => s.Exists()))
        {
            swat.Tasks.Clear();
            swat.Tasks.GoStraightToPosition(bankLocation.Around(10f), 1.0f, 2.0f, 0f, 5000);
        }

        // Robbers react to breach
        foreach (var robber in robbers.Where(r => r.Exists()))
        {
            robber.Tasks.Clear();
            robber.Tasks.FightAgainst(playerCharacter);
        }

        DisplayNotification("Breach in progress! Secure the hostages!");
    }

    private void HandleNegotiation()
    {
        if (playerCharacter.DistanceTo(bankLocation) < 20f)
        {
            if (Game.GameTime % 8000 < 100) // Every 8 seconds
            {
                DisplayNotification("[Numpad1] Demand surrender | [Numpad2] Offer negotiation");
            }

            if (Game.IsKeyDown(settings.NegotiationOption1Key)) // Demand surrender
            {
                HandleDemandSurrender();
            }
            else if (Game.IsKeyDown(settings.NegotiationOption2Key)) // Offer negotiation
            {
                HandleOfferNegotiation();
            }
        }
    }

    private void HandleDemandSurrender()
    {
        negotiationPhase = false;
        int outcome = rand.Next(0, 4);

        if (outcome == 0) // Success
        {
            DisplayNotification("Robbers are surrendering!");
            foreach (var robber in robbers.Where(r => r.Exists()))
            {
                robber.Tasks.Clear();
                robber.Tasks.PutHandsUp(99999, playerCharacter);
            }
            ReleaseHostages();
        }
        else // Failure - they escape
        {
            DisplayNotification("Robbers rejected demands! They're making a run for it!");
            StartEscapeSequence();
        }
    }

    private void HandleOfferNegotiation()
    {
        negotiationPhase = false;
        int outcome = rand.Next(0, 3);

        if (outcome <= 1) // Better success chance
        {
            DisplayNotification("Negotiation successful! Robbers releasing hostages!");
            foreach (var robber in robbers.Where(r => r.Exists()))
            {
                robber.Tasks.Clear();
                robber.Tasks.PutHandsUp(99999, playerCharacter);
            }
            ReleaseHostages();
        }
        else
        {
            DisplayNotification("Negotiation failed! Robbers are fleeing with hostages!");
            StartEscapeSequence();
        }
    }

    private void StartEscapeSequence()
    {
        negotiationPhase = false;
        robbersEscaping = true;

        DisplayNotification("Robbers are heading for their getaway vehicle!");

        GameFiber.StartNew(() =>
        {
            // Move robbers to getaway vehicle
            foreach (var robber in robbers.Where(r => r.Exists()).Take(2)) // Only first 2 robbers escape
            {
                if (getawayVehicle?.Exists() == true)
                {
                    robber.Tasks.Clear();
                    robber.Tasks.EnterVehicle(getawayVehicle, -1);
                }
            }

            GameFiber.Sleep(5000);

            // Start pursuit if robbers got to vehicle
            if (getawayVehicle?.Exists() == true && robbers.Any(r => r.IsInVehicle(getawayVehicle, false)))
            {
                DisplayNotification("Robbers escaped! Pursuit initiated!");
                var driver = robbers.FirstOrDefault(r => r.IsInVehicle(getawayVehicle, false));
                if (driver != null)
                {
                    driver.Tasks.CruiseWithVehicle(getawayVehicle, 45f, VehicleDrivingFlags.Emergency);
                    currentPursuit = Functions.CreatePursuit();
                    Functions.AddPedToPursuit(currentPursuit, driver);
                    Functions.SetPursuitIsActiveForPlayer(currentPursuit, true);
                }
            }
        });
    }

    private void ReleaseHostages()
    {
        foreach (var hostage in hostages.Where(h => h.Exists()))
        {
            hostage.Tasks.Clear();
            hostage.Tasks.ReactAndFlee(robbers.FirstOrDefault());
        }
    }

    private void HandleEscapePhase()
    {
        // Monitor pursuit
        if (currentPursuit != null && !Functions.IsPursuitStillRunning(currentPursuit))
        {
            robbersEscaping = false;
        }
    }

    private void CheckCompletionConditions()
    {
        bool robbersNeutralized = robbers.All(r => !r.Exists() || r.IsDead || Functions.IsPedArrested(r));
        bool hostagesSafe = hostages.All(h => !h.Exists() || h.IsAlive);

        if (robbersNeutralized && hostagesSafe)
        {
            DisplayNotification(LanguageManager.GetText("BANK_SECURED"));
            this.End();
        }
        else if (robbersNeutralized && !hostagesSafe)
        {
            DisplayNotification(LanguageManager.GetText("CALLOUT_FAILED", "Bank Robbery"));
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