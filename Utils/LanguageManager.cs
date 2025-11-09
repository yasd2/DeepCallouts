using Rage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

public static class LanguageManager
{
    private static Dictionary<string, string> translations = new Dictionary<string, string>();
    private static string currentLanguage = "en";

    public static void LoadLanguage(string languageCode)
    {
        currentLanguage = languageCode;
        translations.Clear();

        string languageFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins", "LSPDFR", "DeepCallouts", "Languages", $"{languageCode}.xml");

        if (File.Exists(languageFile))
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(languageFile);

                foreach (XmlNode node in doc.SelectNodes("//string"))
                {
                    string key = node.Attributes["key"]?.Value;
                    string value = node.InnerText;

                    if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                    {
                        translations[key] = value;
                    }
                }
                Game.LogTrivial($"DeepCallouts: Loaded {translations.Count} translations for {languageCode}");
            }
            catch (Exception e)
            {
                Game.LogTrivial($"DeepCallouts ERROR: Failed to load language file {languageCode}: {e.Message}");
                LoadDefaultLanguage();
            }
        }
        else
        {
            Game.LogTrivial($"DeepCallouts: Language file not found for {languageCode}, using default");
            LoadDefaultLanguage();
        }
    }
    private static void LoadDefaultLanguage()
    {
        // Default English translations
        translations["PLUGIN_INITIALIZED"] = "DeepCallouts has been initialized.";
        translations["PLUGIN_DISABLED"] = "DeepCallouts is disabled in INI.";
        translations["HOSTAGE_SITUATION"] = "Hostage Situation";
        translations["CHILD_ABDUCTION"] = "Child Abduction";
        translations["SCHOOL_LOCKDOWN"] = "School Lockdown";
        translations["RESPOND_CODE3"] = "Respond Code 3!";
        translations["HOSTAGE_DESC"] = "Armed suspect holding hostage";
        translations["HOSTAGE_RESPOND"] = "Proceed to hostage situation. Approach with caution!";
        translations["HOSTAGE_SCENE_ACTIVE"] = "Hostage situation active. Assess the situation.";
        translations["HOSTAGE_SAVED"] = "Hostage saved successfully!";
        translations["SUSPECT"] = "Suspect";
        translations["HOSTAGE"] = "Hostage";
        translations["NEGOTIATION_SUCCESSFUL"] = "Negotiations successful!";
        translations["NEGOTIATION_FAILED"] = "Negotiations failed!";
        translations["SUSPECT_ARRESTED"] = "Suspect arrested.";
        translations["CALLOUT_COMPLETED"] = "{0} completed successfully!";
        translations["CALLOUT_FAILED"] = "{0} failed!";

        // NEW Child Abduction translations
        translations["ABDUCTION_DESC"] = "Child abducted by unknown suspect";
        translations["ABDUCTION_RESPOND"] = "Respond to child abduction scene immediately!";
        translations["ABDUCTION_SCENE_ACTIVE"] = "Suspect vehicle located. Approach with caution!";
        translations["CHILD_SAVED"] = "Child rescued successfully!";
        translations["CHILD_LOST"] = "Child was not saved.";
        translations["SUSPECT_VEHICLE"] = "Suspect Vehicle";
        translations["ABDUCTED_CHILD"] = "Abducted Child";
        translations["SUSPECT_FLEEING"] = "Suspect is fleeing! Pursuit initiated!";
        translations["SUSPECT_AT_HOUSE"] = "Suspect reached their house! Prepare for negotiation.";
        translations["SUSPECT_HOUSE"] = "Suspect's House";
        translations["NEGOTIATION_START"] = "Negotiation phase initiated. Convince suspect to release the child!";
        translations["NEGOTIATE_OPTIONS"] = "[Numpad1] Be assertive | [Numpad2] Offer leniency";

        // School Lockdown translations
        translations["LOCKDOWN_DESC"] = "Active shooter reported on campus";
        translations["LOCKDOWN_RESPOND"] = "Respond to University of San Andreas immediately! Active shooter situation!";
        translations["LOCKDOWN_SUCCESS"] = "School lockdown resolved! All civilians evacuated safely.";
        translations["SWAT_COMMANDER"] = "SWAT Commander";
        translations["TALK_TO_COMMANDER"] = "Talk to the SWAT Commander (blue blip) to coordinate the response.";
        translations["COMMANDER_BRIEFING"] = "Commander: Multiple shooters reported inside. Your objective: Evacuate civilians and neutralize threats.";
        translations["CONFIRM_BREACH"] = "Press [E] to confirm breach plan. Entry in {0} seconds.";
        translations["BREACH_COUNTDOWN"] = "Breach confirmed. Entering building in 3... 2... 1...";
        translations["SHOOTERS_DETECTED"] = "ALERT: {0} shooter{1} detected!";
        translations["SHOOTER"] = "Shooter {0}";
        translations["BREACH_ACTIVE"] = "Breach initiated! Evacuate civilians [B] and neutralize shooters [Y].";
        translations["CIVILIAN_EVACUATED"] = "Civilian evacuated! Total: {0}";
        translations["SHOOTER_COMMANDS"] = "Shooter in sight! [Y] to yell commands.";
        translations["SHOOTER_SURRENDERS"] = "Shooter surrendering! Secure them.";
        translations["SHOOTER_HOSTILE"] = "Shooter not cooperating! Take cover!";

        // Bank Robbery translations
        translations["BANK_ROBBERY"] = "Bank Robbery";
        translations["BANK_ROBBERY_DESC"] = "Armed suspects robbing bank with hostages";
        translations["BANK_ROBBERY_RESPOND"] = "Respond to bank robbery in progress! Multiple suspects with hostages!";
        translations["BANK_SECURED"] = "Bank secured! All hostages safe.";
        translations["BANK_ROBBER"] = "Bank Robber";

        // Home Invasion translations
        translations["HOME_INVASION"] = "Home Invasion";
        translations["HOME_INVASION_DESC"] = "Armed suspects have invaded a residence";
        translations["HOME_INVASION_RESPOND"] = "Respond to home invasion! Suspects are armed and dangerous!";
        translations["HOME_SECURED"] = "Residence secured! Homeowners safe.";
        translations["HOME_INVADER"] = "Home Invader";
        translations["HOME_OWNER"] = "Home Owner";

        // Suicide Situation translations
        translations["SUICIDE_SITUATION"] = "Suicide Situation";
        translations["SUICIDE_DESC"] = "Person threatening self-harm";
        translations["SUICIDE_RESPOND"] = "Respond to suicide situation! Handle with extreme care and sensitivity.";
        translations["SUICIDE_SAVED"] = "Person talked down successfully! Crisis averted.";
        translations["SUICIDE_FAILED"] = "Unable to prevent tragedy. Crisis counseling needed.";
        translations["SUICIDE_VICTIM"] = "Person in Crisis";
        translations["COUNSELOR"] = "Crisis Counselor";
        translations["TALK_DOWN_OPTIONS"] = "[Numpad1] Be empathetic | [Numpad2] Call for counselor";

        // Additional emotional suicide situation translations
        translations["CRISIS_ACTIVE"] = "Crisis situation active. Handle with extreme care.";
        translations["APPROACH_SLOWLY"] = "Approach slowly and speak calmly. This person's life depends on your words.";
        translations["EMPATHY_APPROACH"] = "Show genuine empathy and understanding.";
        translations["COUNSELOR_ARRIVING"] = "Crisis counselor is en route to assist.";
        translations["PROGRESS_MADE"] = "Making progress - keep talking, don't give up.";
        translations["RESOURCES_PROVIDED"] = "Mental health resources and support services contacted.";
        translations["LIFELINE_NUMBER"] = "National Suicide Prevention Lifeline: 988";

        // Enhanced AI Commands translations
        translations["AI_BACKUP_REQUESTED"] = "Backup units requested - ETA 45 seconds";
        translations["AI_SWAT_DEPLOYED"] = "SWAT team deployed and taking tactical positions";
        translations["AI_MEDICAL_CALLED"] = "Medical support called - Ambulance en route";
        translations["AI_PERIMETER_SET"] = "Perimeter established - Area secured";
        translations["AI_BACKUP_ARRIVED"] = "Backup units have arrived on scene";
        translations["AI_SWAT_READY"] = "SWAT team in position and ready";
        translations["AI_MEDICAL_READY"] = "Medical support standing by";
        translations["AI_INSUFFICIENT_UNITS"] = "Insufficient units available for this operation";
        translations["AI_MAX_BACKUP"] = "Maximum backup units already deployed";
        translations["AI_PERIMETER_ACTIVE"] = "Perimeter already established";
        translations["AI_COMMANDS_HELP"] = "[R] Request Backup | [T] SWAT Coordination | [M] Medical Support | [P] Set Perimeter";

        // Additional supported languages
        translations["LANGUAGE_SPANISH"] = "Español";
        translations["LANGUAGE_CHINESE"] = "中文";
        translations["LANGUAGE_FRENCH"] = "Français";
        translations["LANGUAGE_GERMAN"] = "Deutsch";
    }

    public static string GetText(string key, params object[] args)
    {
        if (translations.ContainsKey(key))
        {
            return args.Length > 0 ? string.Format(translations[key], args) : translations[key];
        }
        return key; // Return the key if translation not found
    }
}