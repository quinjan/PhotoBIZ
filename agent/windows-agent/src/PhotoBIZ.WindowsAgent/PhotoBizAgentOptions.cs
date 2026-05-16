namespace PhotoBIZ.WindowsAgent;

public sealed class PhotoBizAgentOptions
{
    public string ApiBaseUrl { get; set; } = "http://localhost:5082";
    public string BoothCode { get; set; } = string.Empty;
    public string AgentCredential { get; set; } = string.Empty;
    public int PollIntervalSeconds { get; set; } = 5;
    public int SimulatedSessionDurationSeconds { get; set; } = 6;
}
