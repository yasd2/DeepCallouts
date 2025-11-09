using LSPD_First_Response.Mod.API;
using LSPD_First_Response.Mod.Callouts;
using Rage;
using System.Drawing;
using System.Runtime;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Rebar;

[CalloutInfo("HostageSituation", CalloutProbability.Medium)]
public class HostageSituationCallout : DeepCalloutBase
{
    private Ped suspect;
    private Ped hostage;
#pragma warning disable CS0169
    private Vehicle escapeCar;
#pragma warning restore CS0169
    private Vector3 sceneLocation;
    private bool escapePhase = false;
    private bool negotiationActive = false;

    public override bool OnBeforeCalloutDisplayed()
    {
        if (!base.OnBeforeCalloutDisplayed()) return false;

        // Set callout location
        sceneLocation = playerCharacter.Position.Around(100f, 200f);
        sceneLocation = World.GetNextPositionOnStreet(sceneLocation);

        if (sceneLocation == Vector3.Zero) return false;

        // Set callout properties
        this.CalloutMessage = LanguageManager.GetText("HOSTAGE_SITUATION");
        this.CalloutPosition = sceneLocation;
        this.CalloutAdvisory = LanguageManager.GetText("HOSTAGE_DESC");

        Functions.PlayScannerAudioUsingPosition("ATTENTION_ALL_UNITS WE_HAVE CRIME_HOSTAGE_SITUATION IN_OR_ON_POSITION", sceneLocation);

        return true;
    }

    public override bool OnCalloutAccepted()
    {
        // Create initial blip
        CreateBlip(sceneLocation, BlipSprite.Enemy, Color.Red, LanguageManager.GetText("HOSTAGE_SITUATION"));

        DisplayNotification(LanguageManager.GetText("HOSTAGE_RESPOND"));

        return base.OnCalloutAccepted();
    }

    public override void Process()
    {
        base.Process();
        ProcessEnhancedAI();

        // Check if player is close enough to start the scenario
        if (playerCharacter.DistanceTo(sceneLocation) < 50f && suspect == null)
        {
            StartHostageScenario();
        }

        // Handle negotiation phase
        if (negotiationActive && suspect != null && suspect.Exists())
        {
            HandleNegotiation();
        }

        // Handle escape phase
        if (escapePhase && suspect != null && suspect.Exists())
        {
            HandleEscapePhase();
        }

        // Handle callout completion
        if (suspect != null && (!suspect.Exists() || suspect.IsDead || Functions.IsPedArrested(suspect)))
        {
            if (hostage != null && hostage.Exists() && hostage.IsAlive)
            {
                DisplayNotification(LanguageManager.GetText("HOSTAGE_SAVED"));
                this.End();
            }
            else
            {
                DisplayNotification(LanguageManager.GetText("CALLOUT_FAILED", "Hostage Situation"));
                this.End();
            }
        }
    }

    private void StartHostageScenario()
    {
        // Spawn suspect
        suspect = SpawnPed(settings.HostageSuspectModel, sceneLocation.Around(10f), 0f, true, true);
        if (suspect == null) { DisplayNotification("Failed to spawn suspect!", true); this.End(); return; }

        suspect.Accuracy = 50;
        suspect.Inventory.GiveNewWeapon(WeaponHash.AssaultRifle, 999, true);
        CreateBlip(suspect.Position, BlipSprite.Enemy, Color.Red, LanguageManager.GetText("SUSPECT"));

        // Spawn hostage
        hostage = SpawnPed(settings.HostageModel, sceneLocation.Around(5f), 0f, true, true);
        if (hostage == null) { DisplayNotification("Failed to spawn hostage!", true); this.End(); return; }

        CreateBlip(hostage.Position, BlipSprite.Friend, Color.Yellow, LanguageManager.GetText("HOSTAGE"));

        // Spawn escape car nearby
        Vector3 escapeCarLocation = World.GetNextPositionOnStreet(sceneLocation.Around(settings.HostageEscapeCarDistance));
        if (escapeCarLocation != Vector3.Zero)
        {
            escapeCar = SpawnVehicle(settings.HostageEscapeCarModel, escapeCarLocation, 0f, true);
            if (escapeCar?.Exists() == true)
            {
                CreateBlip(escapeCar.Position, BlipSprite.GetawayCar, Color.Orange, "Escape Vehicle");
            }
        }

        DisplayNotification(LanguageManager.GetText("HOSTAGE_SCENE_ACTIVE"));

        // Start negotiation phase after a delay
        GameFiber.StartNew(() =>
        {
            GameFiber.Sleep(5000); // Give player time to assess
            StartNegotiation();
        });
    }

    private void StartNegotiation()
    {
        negotiationActive = true;
        DisplayNotification("Negotiation started! [Numpad1] Aggressive approach | [Numpad2] Peaceful approach");

        // Suspect aims at hostage
        if (suspect?.Exists() == true && hostage?.Exists() == true)
        {
            suspect.Tasks.AimWeaponAt(hostage, -1);
            hostage.Tasks.PutHandsUp(99999, playerCharacter);
        }
    }

    private void HandleNegotiation()
    {
        if (playerCharacter.DistanceTo(suspect) < 15f)
        {
            // Show negotiation options periodically
            if (Game.GameTime % 5000 < 100) // Every 5 seconds
            {
                DisplayNotification("[Numpad1] Be aggressive | [Numpad2] Negotiate peacefully");
            }

            // Handle player input
            if (Game.IsKeyDown(settings.NegotiationOption1Key)) // Aggressive
            {
                HandleAggressiveNegotiation();
            }
            else if (Game.IsKeyDown(settings.NegotiationOption2Key)) // Peaceful
            {
                HandlePeacefulNegotiation();
            }
        }

        // Timeout - suspect gets nervous and tries to escape
        if (negotiationActive && Game.GameTime % 30000 < 100) // After 30 seconds
        {
            DisplayNotification("Suspect is getting nervous! He's making a move!");
            StartEscapeSequence();
        }
    }

    private void HandleAggressiveNegotiation()
    {
        negotiationActive = false;
        int outcome = rand.Next(0, 3);

        if (outcome == 0) // Success
        {
            DisplayNotification(LanguageManager.GetText("NEGOTIATION_SUCCESSFUL"));
            if (suspect?.Exists() == true)
            {
                suspect.Tasks.Clear();
                suspect.Tasks.PutHandsUp(10000, playerCharacter);
            }
            if (hostage?.Exists() == true)
            {
                GameFiber.StartNew(() => {
                    GameFiber.Sleep(2000);
                    if (hostage?.Exists() == true)
                    {
                        hostage.Tasks.Clear();
                        hostage.Tasks.ReactAndFlee(suspect);
                    }
                });
            }
        }
        else // Failure - suspect panics
        {
            DisplayNotification(LanguageManager.GetText("NEGOTIATION_FAILED"));
            StartEscapeSequence();
        }
    }

    private void HandlePeacefulNegotiation()
    {
        negotiationActive = false;
        int outcome = rand.Next(0, 4);

        if (outcome <= 1) // Higher success chance
        {
            DisplayNotification(LanguageManager.GetText("NEGOTIATION_SUCCESSFUL"));
            if (suspect?.Exists() == true)
            {
                suspect.Tasks.Clear();
                suspect.Tasks.PutHandsUp(15000, playerCharacter);
            }
            if (hostage?.Exists() == true)
            {
                GameFiber.StartNew(() => {
                    GameFiber.Sleep(2000);
                    if (hostage?.Exists() == true)
                    {
                        hostage.Tasks.Clear();
                        hostage.Tasks.ReactAndFlee(suspect);
                    }
                });
            }

        }
        else // Failure - suspect tries to escape
        {
            DisplayNotification("Suspect doesn't trust you! He's making a run for it!");
            StartEscapeSequence();
        }
    }


    private void StartEscapeSequence()
    {
        negotiationActive = false;
        escapePhase = true;

        DisplayNotification("Suspect is heading for the escape vehicle with the hostage!");

        // Suspect grabs hostage and runs to car
        suspect.Tasks.Clear();
        hostage.Tasks.Clear();

        GameFiber.StartNew(() =>
        {
            // Make hostage follow suspect
            hostage.Tasks.FollowToOffsetFromEntity(suspect, Vector3.Zero);

            // Suspect runs to escape car
            if (escapeCar != null && escapeCar.Exists())
            {
                suspect.Tasks.EnterVehicle(escapeCar, -1); // -1 = Driver

                // Wait for suspect to get in car
                GameFiber.Sleep(3000);

                if (suspect.IsInVehicle(escapeCar, false))
                {
                    // Put hostage in car
                    hostage.WarpIntoVehicle(escapeCar, 0); // 0 = Passenger

                    // Start pursuit
                    DisplayNotification("Suspect is fleeing with the hostage! Pursuit initiated!");
                    suspect.Tasks.CruiseWithVehicle(escapeCar, 30f, VehicleDrivingFlags.Emergency);

                    currentPursuit = Functions.CreatePursuit();
                    Functions.AddPedToPursuit(currentPursuit, suspect);
                    Functions.SetPursuitIsActiveForPlayer(currentPursuit, true);
                }
            }
        });
    }

    private void HandleEscapePhase()
    {
        // Monitor the pursuit
        if (currentPursuit != null && !Functions.IsPursuitStillRunning(currentPursuit))
        {
            escapePhase = false;

            // Check if hostage is still alive
            if (hostage != null && hostage.Exists() && hostage.IsAlive)
            {
                DisplayNotification("Pursuit ended! Check on the hostage!");
            }
        }

        // If escape car is destroyed or suspect exits
        if (escapeCar != null && (!escapeCar.Exists() || escapeCar.IsDead))
        {
            DisplayNotification("Suspect's escape vehicle is disabled!");
            escapePhase = false;
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