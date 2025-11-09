using LSPD_First_Response.Mod.API;
using LSPD_First_Response.Mod.Callouts;
using Rage;
using System;
using System.Collections.Generic;
using System.Drawing;

[CalloutInfo("SuicideSituation", CalloutProbability.Low)]
public class SuicideSituationCallout : DeepCalloutBase
{
    private Vector3 crisisLocation;
    private Ped victim;
    private Ped counselor;
    private List<Ped> bystanders = new List<Ped>();
    private bool negotiationActive = false;
    private bool counselorCalled = false;
    private bool crisisResolved = false;
    private int emotionalState = 1; // 1 = Desperate, 2 = Angry, 3 = Hopeless, 4 = Confused
    private string backstory = "";
    private string victimName = "";
    private DateTime negotiationStartTime;
    private int timeLimit;

    public override bool OnBeforeCalloutDisplayed()
    {
        if (!base.OnBeforeCalloutDisplayed()) return false;

        // Find crisis location (bridge, rooftop, etc.)
        crisisLocation = GetCrisisLocation();
        if (crisisLocation == Vector3.Zero) return false;

        // Generate emotional backstory
        GenerateBackstory();

        // Set callout properties
        this.CalloutMessage = LanguageManager.GetText("SUICIDE_SITUATION");
        this.CalloutPosition = crisisLocation;
        this.CalloutAdvisory = LanguageManager.GetText("SUICIDE_DESC");

        Functions.PlayScannerAudioUsingPosition("ATTENTION_ALL_UNITS WE_HAVE CRIME_SUICIDE_ATTEMPT IN_OR_ON_POSITION", crisisLocation);

        return true;
    }

    public override bool OnCalloutAccepted()
    {
        CreateBlip(crisisLocation, BlipSprite.Waypoint, Color.Purple, LanguageManager.GetText("SUICIDE_SITUATION"));
        DisplayNotification(LanguageManager.GetText("SUICIDE_RESPOND"));
        DisplayNotification("~r~HANDLE WITH EXTREME SENSITIVITY~w~ - Person's life depends on your approach.");
        return base.OnCalloutAccepted();
    }

    public override void Process()
    {
        base.Process();

        // Start scenario when player gets close
        if (playerCharacter.DistanceTo(crisisLocation) < 50f && victim == null)
        {
            StartCrisisScenario();
        }

        // Handle negotiation phase
        if (negotiationActive)
        {
            HandleNegotiation();
        }

        // Check time limit
        if (negotiationActive && (DateTime.Now - negotiationStartTime).TotalSeconds > timeLimit)
        {
            HandleTimeOut();
        }

        // Check resolution
        if (crisisResolved)
        {
            this.End();
        }
    }

    private Vector3 GetCrisisLocation()
    {
        // Try to find a bridge or elevated location
        Vector3[] potentialLocations = {
            new Vector3(-312.3f, -2715.0f, 69.0f), // Del Perro Pier
            new Vector3(-1368.0f, -1286.0f, 4.0f),  // Beach pier
            new Vector3(-544.0f, -1289.0f, 26.0f),  // Building rooftop
            new Vector3(120.0f, -1285.0f, 29.0f),   // Downtown building
            playerCharacter.Position.Around(100f, 300f) // Fallback to nearby location
        };

        foreach (var location in potentialLocations)
        {
            if (location != Vector3.Zero)
            {
                return World.GetNextPositionOnStreet(location);
            }
        }

        return playerCharacter.Position.Around(150f, 250f);
    }

    private void GenerateBackstory()
    {
        string[] names = { "Michael", "Sarah", "David", "Emily", "James", "Lisa", "Robert", "Jennifer", "William", "Amanda" };
        victimName = names[rand.Next(names.Length)];

        string[] backstories = {
            $"{victimName} lost their job 3 months ago and can't find work. The bills are piling up, and they're about to lose their home. They feel like a failure and a burden to their family.",

            $"{victimName}'s spouse of 15 years just filed for divorce and is taking the kids. They feel completely alone and like they've lost everything that mattered to them.",

            $"{victimName} has been battling severe depression for years. The medication stopped working, and they can't afford therapy anymore. They're tired of fighting and feel hopeless.",

            $"{victimName} was recently diagnosed with a terminal illness. They're scared of the pain and don't want to be a burden on their loved ones.",

            $"{victimName} lost their teenage child in a car accident 6 months ago. The guilt and grief are overwhelming - they blame themselves for letting their child drive that night.",

            $"{victimName} has been struggling with addiction and just relapsed after 2 years sober. They're ashamed and feel like they've let everyone down again.",

            $"{victimName} came out to their family last week and was completely rejected. Their parents disowned them, and they feel like they have nowhere to turn.",

            $"{victimName} was sexually assaulted and is struggling with PTSD. They feel broken and like they'll never be the same person again.",

            $"{victimName} has been bullied at work for months. Their boss is making their life hell, and HR won't help. They feel trapped and powerless.",

            $"{victimName} is a veteran struggling with combat PTSD. The nightmares won't stop, and they feel disconnected from civilian life. They think their family would be better off without them."
        };

        backstory = backstories[rand.Next(backstories.Length)];
        emotionalState = rand.Next(1, 5);
        timeLimit = settings.SuicideNegotiationTimeLimit;
    }

    private void StartCrisisScenario()
    {
        // Spawn victim
        victim = SpawnPed(settings.SuicideVictimModel, crisisLocation, 0f, true, true);
        if (victim == null) { DisplayNotification("Failed to spawn person in crisis!", true); this.End(); return; }

        CreateBlip(victim.Position, BlipSprite.Waypoint, Color.Purple, LanguageManager.GetText("SUICIDE_VICTIM"));

        // Make victim look distressed
        victim.Tasks.StandStill(-1);

        // Spawn concerned bystanders
        for (int i = 0; i < rand.Next(2, 5); i++)
        {
            Vector3 bystanderPos = crisisLocation.Around(rand.Next(15, 30));
            Ped bystander = SpawnPed("A_M_M_BUSINESS_01", bystanderPos, 0f, true, true);
            if (bystander != null)
            {
                bystanders.Add(bystander);
                float heading = MathHelper.ConvertDirectionToHeading(victim.Position - bystander.Position);
                bystander.Tasks.AchieveHeading(heading, 2000);
            }
        }

        DisplayNotification("~y~CRISIS SITUATION ACTIVE~w~");
        DisplayNotification($"~r~Person in distress: {victimName}~w~");
        DisplayNotification("Approach slowly and calmly. Your words can save a life.");

        GameFiber.Sleep(3000);
        DisplayNotification("~o~BACKSTORY:~w~ " + backstory);

        GameFiber.StartNew(() =>
        {
            GameFiber.Sleep(5000);
            if (victim?.Exists() == true)
            {
                StartInitialContact();
            }
        });
    }

    private void StartInitialContact()
    {
        negotiationActive = true;
        negotiationStartTime = DateTime.Now;

        DisplayNotification($"~p~{victimName}:~w~ " + GetInitialStatement());
        DisplayNotification("~g~APPROACH CAREFULLY~w~ - [Numpad1] Show empathy | [Numpad2] Call crisis counselor");
        DisplayNotification($"~y~Time remaining: {timeLimit} seconds~w~");
    }

    private string GetInitialStatement()
    {
        string[] statements = {
            "Stay back! I don't want to hurt anyone, but I can't do this anymore...",
            "Please... just leave me alone. I've made up my mind. There's nothing left for me.",
            "You don't understand what I've been through! Nobody does!",
            "I'm tired of fighting... I'm tired of pretending everything's okay when it's not.",
            "My family... they'd be better off without me. I'm just a burden to everyone.",
            "I can't take the pain anymore. Every day is a struggle just to breathe...",
            "What's the point? Everything I touch turns to ash. I ruin everything.",
            "The voices in my head won't stop... telling me I'm worthless, that I should just end it."
        };

        return statements[rand.Next(statements.Length)];
    }

    private void HandleNegotiation()
    {
        if (playerCharacter.DistanceTo(victim) < 15f)
        {
            // Show options periodically
            if (Game.GameTime % 10000 < 100) // Every 10 seconds
            {
                int timeRemaining = timeLimit - (int)(DateTime.Now - negotiationStartTime).TotalSeconds;
                DisplayNotification($"~y~Time: {timeRemaining}s~w~ | [Numpad1] Be empathetic | [Numpad2] Call counselor");
            }

            // Handle player input
            if (Game.IsKeyDown(settings.NegotiationOption1Key)) // Empathetic approach
            {
                HandleEmpatheticApproach();
            }
            else if (Game.IsKeyDown(settings.NegotiationOption2Key)) // Call counselor
            {
                CallCrisisCounselor();
            }
        }
        else if (playerCharacter.DistanceTo(victim) > 20f)
        {
            DisplayNotification("~r~You're too far away!~w~ Get closer but move slowly and calmly.");
        }
    }

    private void HandleEmpatheticApproach()
    {
        if (counselorCalled)
        {
            DisplayNotification("Wait for the crisis counselor to arrive and take the lead.");
            return;
        }

        string[] empatheticResponses = {
            $"Officer: \"{victimName}, I can see you're in incredible pain right now. I'm here to listen, not to judge.\"",
            $"Officer: \"What you're feeling is valid, {victimName}. You matter, and your life has value.\"",
            $"Officer: \"I know it feels hopeless right now, but this feeling won't last forever. Let's talk through this.\"",
            $"Officer: \"You mentioned your family - they love you, even if it doesn't feel that way right now.\"",
            $"Officer: \"You're not alone in this, {victimName}. There are people who want to help you through this pain.\"",
            $"Officer: \"I can't imagine how much you're hurting, but ending your life isn't the answer. Let's find another way.\"",
            $"Officer: \"You've survived difficult times before, {victimName}. You're stronger than you realize.\""
        };

        DisplayNotification("~g~" + empatheticResponses[rand.Next(empatheticResponses.Length)] + "~w~");

        GameFiber.StartNew(() =>
        {
            GameFiber.Sleep(3000);
            HandleVictimResponse();
        });
    }

    private void HandleVictimResponse()
    {
        int responseType = rand.Next(1, 4);

        switch (emotionalState)
        {
            case 1: // Desperate
                if (responseType == 1)
                {
                    DisplayNotification($"~p~{victimName}:~w~ \"You don't understand... I've lost everything. My job, my home, my dignity...\"");
                    emotionalState = 2; // Moving to angry
                }
                else
                {
                    DisplayNotification($"~p~{victimName}:~w~ \"Maybe... maybe you're right. But how do I go on when everything hurts so much?\"");
                    ProgressTowardsResolution();
                }
                break;

            case 2: // Angry
                if (responseType == 1)
                {
                    DisplayNotification($"~p~{victimName}:~w~ \"Don't give me that bullshit! You cops are all the same - you don't care!\"");
                    // Stays angry
                }
                else
                {
                    DisplayNotification($"~p~{victimName}:~w~ \"I'm sorry for yelling... I'm just so angry at myself, at the world...\"");
                    emotionalState = 3; // Moving to hopeless
                }
                break;

            case 3: // Hopeless
                if (responseType == 1)
                {
                    DisplayNotification($"~p~{victimName}:~w~ \"It's too late for me... I've made too many mistakes. I can't fix this.\"");
                    // Stays hopeless
                }
                else
                {
                    DisplayNotification($"~p~{victimName}:~w~ \"I want to believe you... but I'm so tired of fighting every day.\"");
                    ProgressTowardsResolution();
                }
                break;

            case 4: // Confused
                DisplayNotification($"~p~{victimName}:~w~ \"I don't know what I want anymore... I just want the pain to stop.\"");
                ProgressTowardsResolution();
                break;
        }
    }

    private void CallCrisisCounselor()
    {
        if (counselorCalled) return;

        counselorCalled = true;
        DisplayNotification("~b~Calling crisis counselor...~w~");

        GameFiber.StartNew(() =>
        {
            GameFiber.Sleep(8000); // Counselor takes time to arrive

            Vector3 counselorPos = crisisLocation.Around(20f);
            counselor = SpawnPed(settings.SuicideCounselorModel, counselorPos, 0f, true, true);

            if (counselor != null)
            {
                CreateBlip(counselor.Position, BlipSprite.Friend, Color.Green, LanguageManager.GetText("COUNSELOR"));
                counselor.Tasks.GoStraightToPosition(crisisLocation.Around(8f), 1.0f, 0f, 0f, 10000);

                DisplayNotification("~g~Crisis counselor has arrived and is taking over negotiations.~w~");

                GameFiber.Sleep(5000);
                HandleCounselorNegotiation();
            }
        });
    }

    private void HandleCounselorNegotiation()
    {
        string[] counselorDialogue = {
            $"Counselor: \"Hi {victimName}, my name is Dr. Martinez. I'm here because I care about you and want to help.\"",
            $"Counselor: \"I've helped many people who felt exactly like you do right now. You're not alone in this.\"",
            $"Counselor: \"What you're experiencing is a crisis, not a character flaw. We can work through this together.\"",
            $"Counselor: \"I know everything feels overwhelming, but there are treatments and support systems that can help.\"",
            $"Counselor: \"Your pain is real and valid, but it doesn't have to be permanent. Let's take this one step at a time.\""
        };

        foreach (string dialogue in counselorDialogue)
        {
            DisplayNotification("~g~" + dialogue + "~w~");
            GameFiber.Sleep(4000);
        }

        // Counselor has higher success rate
        int outcome = rand.Next(1, 5); // 80% success rate with counselor

        if (outcome <= 3)
        {
            ResolveCrisisSuccessfully();
        }
        else
        {
            DisplayNotification($"~p~{victimName}:~w~ \"I appreciate you trying, but I've made my decision...\"");
            ContinueNegotiation();
        }
    }

    private void ProgressTowardsResolution()
    {
        int progress = rand.Next(1, 4);

        if (progress <= 2) // 66% chance of positive progress
        {
            ResolveCrisisSuccessfully();
        }
        else
        {
            ContinueNegotiation();
        }
    }

    private void ContinueNegotiation()
    {
        DisplayNotification("~y~Continue talking - don't give up. Every word matters.~w~");
        // Reset some time
        negotiationStartTime = negotiationStartTime.AddSeconds(30);
    }

    private void ResolveCrisisSuccessfully()
    {
        crisisResolved = true;

        DisplayNotification($"~p~{victimName}:~w~ \"Okay... okay. Maybe you're right. I don't want to die... I just want the pain to stop.\"");
        GameFiber.Sleep(2000);
        DisplayNotification($"~p~{victimName}:~w~ \"Will you help me? I don't know how to get through this alone.\"");
        GameFiber.Sleep(2000);
        DisplayNotification("~g~CRISIS RESOLVED SUCCESSFULLY~w~");
        DisplayNotification(LanguageManager.GetText("SUICIDE_SAVED"));

        // Victim moves away from danger
        if (victim?.Exists() == true)
        {
            victim.Tasks.Clear();
            victim.Tasks.GoStraightToPosition(playerCharacter.Position.Around(3f), 1.0f, 0f, 0f, 10000);
        }

        // Show resources
        DisplayNotification("~b~Connecting to mental health resources and emergency services.~w~");
        DisplayNotification("~g~National Suicide Prevention Lifeline: 988~w~");
    }

    private void HandleTimeOut()
    {
        crisisResolved = true;

        DisplayNotification("~r~Time ran out...~w~");
        DisplayNotification($"~p~{victimName}:~w~ \"I'm sorry... I can't do this anymore.\"");
        DisplayNotification(LanguageManager.GetText("SUICIDE_FAILED"));
        DisplayNotification("~r~CRISIS COUNSELING SERVICES NOTIFIED~w~");

        // This is a sensitive topic - we won't show graphic content
        if (victim?.Exists() == true)
        {
            victim.Tasks.Clear();
            victim.Delete(); // Remove from scene respectfully
        }
    }

    public override void End()
    {
        base.End();
    }
}