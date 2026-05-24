using Microsoft.Extensions.Options;

namespace PhotoBIZ.WindowsAgent;

public interface IAgentRuntimeOptionsProvider
{
    Task<PhotoBizAgentOptions> LoadAsync(CancellationToken cancellationToken);
}

public sealed class AgentRuntimeOptionsProvider(
    IAgentConfigurationStore configurationStore,
    IOptions<PhotoBizAgentOptions> configuredOptions) : IAgentRuntimeOptionsProvider
{
    public async Task<PhotoBizAgentOptions> LoadAsync(CancellationToken cancellationToken)
    {
        var saved = await configurationStore.LoadRuntimeOptionsAsync(cancellationToken);
        var configured = configuredOptions.Value;

        if (HasPairing(saved) || !HasPairing(configured))
        {
            return saved;
        }

        return CloneConfiguredDevelopmentPairing(configured, saved);
    }

    private static bool HasPairing(PhotoBizAgentOptions options)
    {
        return !string.IsNullOrWhiteSpace(options.BoothCode) &&
            !string.IsNullOrWhiteSpace(options.AgentCredential);
    }

    private static PhotoBizAgentOptions CloneConfiguredDevelopmentPairing(
        PhotoBizAgentOptions configured,
        PhotoBizAgentOptions savedDefaults)
    {
        var hasSavedLocalSettings = !string.IsNullOrWhiteSpace(savedDefaults.BoothCode);

        return new PhotoBizAgentOptions
        {
            ApiBaseUrl = hasSavedLocalSettings ? savedDefaults.ApiBaseUrl : configured.ApiBaseUrl,
            BoothCode = hasSavedLocalSettings ? savedDefaults.BoothCode : configured.BoothCode,
            BoothName = hasSavedLocalSettings ? savedDefaults.BoothName : configured.BoothName,
            AgentCredential = configured.AgentCredential,
            PollIntervalSeconds = hasSavedLocalSettings
                ? savedDefaults.PollIntervalSeconds
                : configured.PollIntervalSeconds,
            SimulatedSessionDurationSeconds = hasSavedLocalSettings
                ? savedDefaults.SimulatedSessionDurationSeconds
                : configured.SimulatedSessionDurationSeconds,
            Storage = new AgentStorageOptions
            {
                BaseDirectory = string.IsNullOrWhiteSpace(configured.Storage.BaseDirectory)
                    ? savedDefaults.Storage.BaseDirectory
                    : configured.Storage.BaseDirectory
            },
            LumaBooth = new LumaBoothOptions
            {
                Mode = hasSavedLocalSettings ? savedDefaults.LumaBooth.Mode : configured.LumaBooth.Mode,
                ApiBaseUrl = hasSavedLocalSettings ? savedDefaults.LumaBooth.ApiBaseUrl : configured.LumaBooth.ApiBaseUrl,
                ApiPassword = hasSavedLocalSettings && !string.IsNullOrWhiteSpace(savedDefaults.LumaBooth.ApiPassword)
                    ? savedDefaults.LumaBooth.ApiPassword
                    : configured.LumaBooth.ApiPassword,
                TriggerListenerUrl = hasSavedLocalSettings
                    ? savedDefaults.LumaBooth.TriggerListenerUrl
                    : configured.LumaBooth.TriggerListenerUrl,
                StartTimeoutSeconds = hasSavedLocalSettings
                    ? savedDefaults.LumaBooth.StartTimeoutSeconds
                    : configured.LumaBooth.StartTimeoutSeconds
            },
            Display = new DisplayOptions
            {
                LumaBoothWindowTitle = hasSavedLocalSettings
                    ? savedDefaults.Display.LumaBoothWindowTitle
                    : configured.Display.LumaBoothWindowTitle,
                BoothUiWindowTitle = hasSavedLocalSettings
                    ? savedDefaults.Display.BoothUiWindowTitle
                    : configured.Display.BoothUiWindowTitle,
                BoothUiBaseUrl = hasSavedLocalSettings
                    ? savedDefaults.Display.BoothUiBaseUrl
                    : configured.Display.BoothUiBaseUrl,
                ChromeExecutablePath = hasSavedLocalSettings
                    ? savedDefaults.Display.ChromeExecutablePath
                    : configured.Display.ChromeExecutablePath,
                ChromeUserDataDir = ResolveChromeUserDataDir(configured, savedDefaults, hasSavedLocalSettings),
                LaunchBoothUiOnStartup = hasSavedLocalSettings
                    ? savedDefaults.Display.LaunchBoothUiOnStartup
                    : configured.Display.LaunchBoothUiOnStartup,
                KioskMode = hasSavedLocalSettings ? savedDefaults.Display.KioskMode : configured.Display.KioskMode
            }
        };
    }

    private static string ResolveChromeUserDataDir(
        PhotoBizAgentOptions configured,
        PhotoBizAgentOptions savedDefaults,
        bool hasSavedLocalSettings)
    {
        if (hasSavedLocalSettings)
        {
            return savedDefaults.Display.ChromeUserDataDir;
        }

        return string.IsNullOrWhiteSpace(configured.Display.ChromeUserDataDir)
            ? savedDefaults.Display.ChromeUserDataDir
            : configured.Display.ChromeUserDataDir;
    }
}

public sealed class StaticAgentRuntimeOptionsProvider(PhotoBizAgentOptions options) : IAgentRuntimeOptionsProvider
{
    public Task<PhotoBizAgentOptions> LoadAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(options);
    }
}
