namespace PhotoBIZ.WindowsAgent;

public static class AgentMetadata
{
    public const string ServiceName = "PhotoBIZ.WindowsAgent";
    public const string RuntimeKind = "ControlCenter";

    public static string Version { get; } = typeof(AgentMetadata).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";
}
