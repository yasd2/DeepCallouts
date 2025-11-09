using LSPD_First_Response.Mod.API;
using LSPD_First_Response.Mod.Callouts;
using Rage;
using System.Drawing;

[CalloutInfo("ChildAbduction", CalloutProbability.Medium)]
public class ChildAbductionCallout : DeepCalloutBase
{
    private Ped suspect;
    private Ped child;
    private Vehicle suspectVehicle;
    private Vector3 sceneLocation;
    private Vector3 suspectHouseLocation;
    // currentPursuit is now inherited from DeepCalloutBase
    private bool negotiationPhase = false;
    public override bool OnBeforeCalloutDisplayed()
    {
        if (!base.OnBeforeCalloutDisplayed()) return false;

        // Set callout location
        sceneLocation = playerCharacter.Position.Around(100f, 200f);
        sceneLocation = World.GetNextPositionOnStreet(sceneLocation);

        if (sceneLocation == Vector3.Zero) return false;

        // Set suspect house location
        suspectHouseLocation = World.GetNextPositionOnStreet(sceneLocation.Around(500f, 1000f));
        if (suspectHouseLocation == Vector3.Zero) return false;

        // Set callout properties
        this.CalloutMessage = LanguageManager.GetText("CHILD_ABDUCTION");
        this.CalloutPosition = sceneLocation;
        this.CalloutAdvisory = LanguageManager.GetText("ABDUCTION_DESC");

        Functions.PlayScannerAudioUsingPosition("ATTENTION_ALL_UNITS WE_HAVE CRIME_KIDNAPPING IN_OR_ON_POSITION", sceneLocation);

        return true;
    }


    public override bool OnCalloutAccepted()
    {
        // Create initial blip
        CreateBlip(sceneLocation, BlipSprite.Waypoint, Color.Blue, LanguageManager.GetText("CHILD_ABDUCTION"));

        DisplayNotification(LanguageManager.GetText("ABDUCTION_RESPOND"));

        return base.OnCalloutAccepted();
    }

    public override void Process()
    {
        base.Process();

        // Check if player is close enough to start the scenario
        if (playerCharacter.DistanceTo(sceneLocation) < 50f && suspect == null)
        {
            StartAbductionScenario();
        }

        // Handle negotiation phase
        if (negotiationPhase && suspect != null && suspect.Exists())
        {
            HandleNegotiation();
        }

        // Handle callout completion
        if (suspect != null && (!suspect.Exists() || suspect.IsDead || Functions.IsPedArrested(suspect)))
        {
            if (child != null && child.Exists() && child.IsAlive)
            {
                DisplayNotification(LanguageManager.GetText("CHILD_SAVED"));
                this.End();
            }
            else
            {
                DisplayNotification(LanguageManager.GetText("CHILD_LOST"));
                this.End();
            }
        }
    }

    private void StartAbductionScenario()
    {
        // Spawn suspect vehicle
        suspectVehicle = SpawnVehicle(settings.AbductionSuspectVehicleModel, sceneLocation.Around(5f), 0f, true);
        if (suspectVehicle == null) { DisplayNotification("Failed to spawn suspect vehicle!", true); this.End(); return; }

        CreateBlip(suspectVehicle.Position, BlipSprite.Waypoint, Color.Red, LanguageManager.GetText("SUSPECT_VEHICLE"));

        // Spawn suspect
        suspect = SpawnPed(settings.AbductionSuspectModel, suspectVehicle.Position, 0f, true, true);
        if (suspect == null) { DisplayNotification("Failed to spawn suspect!", true); this.End(); return; }

        suspect.Inventory.GiveNewWeapon(WeaponHash.Pistol, 999, true);
        suspect.WarpIntoVehicle(suspectVehicle, -1);
        CreateBlip(suspect.Position, BlipSprite.Enemy, Color.Red, LanguageManager.GetText("SUSPECT"));

        // Spawn child
        child = SpawnPed(settings.AbductionChildModel, suspectVehicle.Position.Around(1f), 0f, true, true);
        if (child == null) { DisplayNotification("Failed to spawn child!", true); this.End(); return; }

        child.WarpIntoVehicle(suspectVehicle, 0);
        CreateBlip(child.Position, BlipSprite.CrateDrop, Color.Yellow, LanguageManager.GetText("ABDUCTED_CHILD"));

        DisplayNotification(LanguageManager.GetText("ABDUCTION_SCENE_ACTIVE"));

        // Start pursuit when player gets close
        GameFiber.StartNew(() =>
        {
            while (playerCharacter.DistanceTo(sceneLocation) > 30f && suspect.Exists())
                GameFiber.Yield(); // Use Yield instead of Sleep for smaller waits within Process

            if (suspect.Exists())
            {
                DisplayNotification(LanguageManager.GetText("SUSPECT_FLEEING"));
                suspect.Tasks.CruiseWithVehicle(suspectVehicle, 50f, VehicleDrivingFlags.Emergency);
                currentPursuit = Functions.CreatePursuit();
                Functions.AddPedToPursuit(currentPursuit, suspect);
                Functions.SetPursuitIsActiveForPlayer(currentPursuit, true);

                MonitorPursuit();
            }
        });
    }

    private void MonitorPursuit()
    {
        GameFiber.StartNew(() =>
        {
            bool suspectReachedHouse = false;

            while (Functions.IsPursuitStillRunning(currentPursuit) && suspect.Exists())
            {
                if (suspectVehicle.Exists() && suspectVehicle.Position.DistanceTo(suspectHouseLocation) < 50f)
                {
                    suspectReachedHouse = true;
                    Functions.ForceEndPursuit(currentPursuit);
                    break;
                }
                GameFiber.Sleep(1000);
            }

            if (suspectReachedHouse && suspect.Exists())
            {
                StartNegotiationPhase();
            }
        });
    }

    private void StartNegotiationPhase()
    {
        DisplayNotification(LanguageManager.GetText("SUSPECT_AT_HOUSE"));
        CreateBlip(suspectHouseLocation, BlipSprite.Safehouse, Color.Yellow, LanguageManager.GetText("SUSPECT_HOUSE"));

        // Suspect exits vehicle
        if (suspect.IsInVehicle(suspectVehicle, false))
        {
            suspect.Tasks.LeaveVehicle(suspectVehicle, LeaveVehicleFlags.None);
            GameFiber.Sleep(2000);
        }

        suspect.Tasks.GoToOffsetFromEntity(playerCharacter, 5f, 0f, 0f);
        suspect.Tasks.AimWeaponAt(playerCharacter, -1);

        negotiationPhase = true;
        DisplayNotification(LanguageManager.GetText("NEGOTIATION_START"));
    }

    private void HandleNegotiation()
    {
        // This would be expanded with full negotiation logic similar to hostage situation
        // For now, simplified version
        if (playerCharacter.DistanceTo(suspect) < 10f)
        {
            DisplayNotification(LanguageManager.GetText("NEGOTIATE_OPTIONS"));
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