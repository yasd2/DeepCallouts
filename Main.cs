using LSPD_First_Response.Mod.API;
using Rage;
using System;
using System.Linq;

public class Main : Plugin
{
    public override void Initialize()
    {
        Game.LogTrivial("DeepCallouts: Starting Initialization");

        try
        {
            AppDomain.CurrentDomain.UnhandledException += UnhandledException;

            Game.LogTrivial("DeepCallouts: Loading settings...");
            DeepCalloutBase.LoadSettings();

            if (!DeepCalloutBase.GetSettings().EnablePlugin)
            {
                Game.LogTrivial("DeepCallouts: Plugin disabled by INI settings.");
                Game.DisplayNotification("~b~DeepCallouts~w~ is ~r~disabled~w~ in INI.");
                return;
            }

            // Determine which language to load
            string languageToLoad = "en"; // Default fallback
            string[] supportedLanguages = { "en", "es", "zh", "fr", "de" }; // Added French and German

            if (DeepCalloutBase.GetSettings().Language == "auto")
            {
                // Auto-detect system language
                string systemLanguage = System.Globalization.CultureInfo.CurrentCulture.TwoLetterISOLanguageName.ToLower();
                languageToLoad = supportedLanguages.Contains(systemLanguage) ? systemLanguage : "en";
            }
            else if (supportedLanguages.Contains(DeepCalloutBase.GetSettings().Language.ToLower()))
            {
                // Use manually configured language
                languageToLoad = DeepCalloutBase.GetSettings().Language.ToLower();
            }

            // Load the determined language
            LanguageManager.LoadLanguage(languageToLoad);

            // Register callouts with a delay to ensure LSPDFR is ready
            GameFiber.StartNew(() =>
            {
                GameFiber.Sleep(5000); // Wait 5 seconds for LSPDFR to fully initialize
                RegisterCallouts();
            });

            Game.LogTrivial("DeepCallouts: Plugin Initialized successfully.");
            Game.DisplayNotification(LanguageManager.GetText("PLUGIN_INITIALIZED"));
        }
        catch (Exception e)
        {
            Game.LogTrivial($"DeepCallouts ERROR: Failed to initialize plugin: {e.Message}");
            Game.LogTrivial($"Stack Trace: {e.StackTrace}");
            Game.DisplayNotification("~r~DeepCallouts failed to initialize. Check the log for details.");
        }
    }

    private static void RegisterCallouts()
    {
        try
        {
            // Register callouts
            Functions.RegisterCallout(typeof(HostageSituationCallout));
            Functions.RegisterCallout(typeof(ChildAbductionCallout));
            Functions.RegisterCallout(typeof(SchoolLockdownCallout));
            Functions.RegisterCallout(typeof(SuicideSituationCallout));
            Functions.RegisterCallout(typeof(BankRobberyCallout));
            Functions.RegisterCallout(typeof(HomeInvasionCallout));

            Game.LogTrivial("DeepCallouts: Callouts registered successfully.");
        }
        catch (Exception e)
        {
            Game.LogTrivial($"DeepCallouts ERROR: Failed to register callouts: {e.Message}");
            Game.LogTrivial($"Stack Trace: {e.StackTrace}");
        }
    }

    public override void Finally()
    {
        Game.LogTrivial("DeepCallouts: Plugin cleaned up.");
    }

    // Standard RPH Unhandled Exception Handler
    private static void UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Exception ex = e.ExceptionObject as Exception;
        string errorMessage = ex != null ? ex.Message : "Unknown error";
        string stackTrace = ex != null ? ex.StackTrace : "";

        Game.LogTrivial($"DeepCallouts crashed: {errorMessage}");
        Game.LogTrivial($"Stack Trace: {stackTrace}");
        Game.DisplayNotification("~r~DeepCallouts has crashed. See the log for more information.");
    }
}