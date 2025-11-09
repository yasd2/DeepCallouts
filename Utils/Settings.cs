using System.Windows.Forms;

public class Settings
{
    // General Settings
    public bool EnablePlugin { get; set; } = true;
    public bool ShowNotifications { get; set; } = true;
    public bool EnableDebugLogging { get; set; } = false;

    // Keybinds
    public string Language { get; set; } = "auto"; // "auto", "en", "es", "zh"
    public Keys NegotiationOption1Key { get; set; } = Keys.NumPad1;
    public Keys NegotiationOption1Modifier { get; set; } = Keys.None;
    public Keys NegotiationOption2Key { get; set; } = Keys.NumPad2;
    public Keys NegotiationOption2Modifier { get; set; } = Keys.None;
    public Keys YellCommandsKey { get; set; } = Keys.Y;
    public Keys YellCommandsModifier { get; set; } = Keys.None;
    public Keys EvacuateStudentKey { get; set; } = Keys.B;
    public Keys EvacuateStudentModifier { get; set; } = Keys.None;

    // Enhanced AI Commands
    public Keys RequestBackupKey { get; set; } = Keys.R;
    public Keys RequestBackupModifier { get; set; } = Keys.None;
    public Keys CoordinateSwatKey { get; set; } = Keys.T;
    public Keys CoordinateSwatModifier { get; set; } = Keys.None;
    public Keys CallAmbulanceKey { get; set; } = Keys.M;
    public Keys CallAmbulanceModifier { get; set; } = Keys.None;
    public Keys SetPerimeterKey { get; set; } = Keys.P;
    public Keys SetPerimeterModifier { get; set; } = Keys.None;

    // AI Enhancement Settings
    public bool EnableEnhancedAI { get; set; } = true;
    public bool EnableDynamicBackup { get; set; } = true;
    public int MaxBackupUnits { get; set; } = 6;
    public int BackupResponseTime { get; set; } = 45; // seconds

    // Hostage Situation
    public int HostageNegotiationTimeLimitSeconds { get; set; } = 180;
    public int CalloutCooldownSeconds { get; set; } = 30;
    public string HostageEscapeCarModel { get; set; } = "FUGITIVE";
    public float HostageEscapeCarDistance { get; set; } = 50.0f;
    public string HostageSuspectModel { get; set; } = "A_M_Y_VINDOUCHE_01";
    public int HostageCount { get; set; } = 1;
    public string HostageModel { get; set; } = "A_F_Y_BUSINESS_02";

    // Child Abduction
    public string AbductionSuspectVehicleModel { get; set; } = "SENTINEL";
    public string AbductionSuspectModel { get; set; } = "A_M_M_PROLHOST_01";
    public string AbductionChildModel { get; set; } = "A_F_Y_HIPSTER_01";
    public int AbductionNegotiationTimeLimitSeconds { get; set; } = 120;

    // School Lockdown
    public float SchoolLockdownLocationX { get; set; } = -1696.866f;
    public float SchoolLockdownLocationY { get; set; } = 142.747f;
    public float SchoolLockdownLocationZ { get; set; } = 64.372f;
    public string SchoolShooterModel { get; set; } = "A_M_Y_VINDOUCHE_01";
    public string SchoolSWATCommanderModel { get; set; } = "S_M_Y_SWAT_01";
    public string SchoolStudentModel { get; set; } = "A_F_Y_HIPSTER_02";
    public string SchoolFacultyModel { get; set; } = "A_M_M_BUSINESS_01";
    public int SchoolBreachDelaySeconds { get; set; } = 10;

    // Bank Robbery
    public float BankRobberyLocationX { get; set; } = 75.38659f;
    public float BankRobberyLocationY { get; set; } = -818.9402f;
    public float BankRobberyLocationZ { get; set; } = 44.57247f;
    public string BankRobberModel { get; set; } = "A_M_M_PROLHOST_01";
    public string BankHostageModel { get; set; } = "A_F_Y_BUSINESS_02";
    public int BankRobberCount { get; set; } = 2;

    // Home Invasion
    public string HomeInvaderModel { get; set; } = "A_M_Y_VINDOUCHE_01";
    public string HomeOwnerModel { get; set; } = "A_M_M_BUSINESS_01";
    public int HomeInvaderCount { get; set; } = 2;

    // Suicide Situation
    public string SuicideVictimModel { get; set; } = "A_M_M_BUSINESS_01";
    public int SuicideNegotiationTimeLimit { get; set; } = 300;
    public string SuicideCounselorModel { get; set; } = "A_F_Y_BUSINESS_02";
}