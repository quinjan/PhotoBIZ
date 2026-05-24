using Microsoft.Extensions.Options;

namespace PhotoBIZ.WindowsAgent;

public interface IAgentDataPaths
{
    string RootDirectory { get; }
    string ConfigurationFilePath { get; }
    string ActiveSessionFilePath { get; }
    string BoothUiLaunchStateFilePath { get; }
}

public sealed class AgentDataPaths(IOptions<PhotoBizAgentOptions> options) : IAgentDataPaths
{
    private readonly AgentStorageOptions storageOptions = options.Value.Storage;

    public string RootDirectory => ResolveRootDirectory(storageOptions.BaseDirectory);

    public string ConfigurationFilePath => Path.Combine(RootDirectory, "config.json");

    public string ActiveSessionFilePath => Path.Combine(RootDirectory, "active-session.json");

    public string BoothUiLaunchStateFilePath => Path.Combine(RootDirectory, "booth-ui-launch.json");

    public static string ResolveRootDirectory(string configuredBaseDirectory)
    {
        if (!string.IsNullOrWhiteSpace(configuredBaseDirectory))
        {
            return configuredBaseDirectory;
        }

        var commonApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return string.IsNullOrWhiteSpace(commonApplicationData)
            ? Path.Combine(Path.GetTempPath(), "PhotoBIZ", "Agent")
            : Path.Combine(commonApplicationData, "PhotoBIZ", "Agent");
    }
}
