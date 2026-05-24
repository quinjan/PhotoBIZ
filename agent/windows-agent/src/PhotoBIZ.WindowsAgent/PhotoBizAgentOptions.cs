namespace PhotoBIZ.WindowsAgent;

public sealed class PhotoBizAgentOptions
{
    public string ApiBaseUrl { get; set; } = "http://localhost:5082";
    public string BoothCode { get; set; } = string.Empty;
    public string BoothName { get; set; } = string.Empty;
    public string AgentCredential { get; set; } = string.Empty;
    public int PollIntervalSeconds { get; set; } = 5;
    public int SimulatedSessionDurationSeconds { get; set; } = 6;
    public AgentStorageOptions Storage { get; set; } = new();
    public LumaBoothOptions LumaBooth { get; set; } = new();
    public DisplayOptions Display { get; set; } = new();
}

public sealed class AgentStorageOptions
{
    public string BaseDirectory { get; set; } = string.Empty;
}

public sealed class LumaBoothOptions
{
    public string Mode { get; set; } = LumaBoothIntegrationMode.Simulator;
    public string ApiBaseUrl { get; set; } = "http://localhost:1500";
    public string ApiPassword { get; set; } = string.Empty;
    public string TriggerListenerUrl { get; set; } = "http://127.0.0.1:5617/lumabooth/events";
    public int StartTimeoutSeconds { get; set; } = 15;
}

public sealed class DisplayOptions
{
    public string LumaBoothWindowTitle { get; set; } = "dslrBooth";
    public string BoothUiWindowTitle { get; set; } = "BoothUi";
    public string BoothUiBaseUrl { get; set; } = "http://localhost:4201";
    public string ChromeExecutablePath { get; set; } = string.Empty;
    public string ChromeUserDataDir { get; set; } = string.Empty;
    public bool LaunchBoothUiOnStartup { get; set; } = true;
    public bool KioskMode { get; set; } = true;
}

public static class LumaBoothIntegrationMode
{
    public const string Simulator = "Simulator";
    public const string Api = "Api";
}
