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

        return HasPairing(saved) || !HasPairing(configured)
            ? saved
            : CloneConfiguredDefaults(configured, saved);
    }

    private static bool HasPairing(PhotoBizAgentOptions options)
    {
        return !string.IsNullOrWhiteSpace(options.BoothCode) &&
            !string.IsNullOrWhiteSpace(options.AgentCredential);
    }

    private static PhotoBizAgentOptions CloneConfiguredDefaults(
        PhotoBizAgentOptions configured,
        PhotoBizAgentOptions savedDefaults)
    {
        return new PhotoBizAgentOptions
        {
            ApiBaseUrl = configured.ApiBaseUrl,
            BoothCode = configured.BoothCode,
            AgentCredential = configured.AgentCredential,
            PollIntervalSeconds = configured.PollIntervalSeconds,
            SimulatedSessionDurationSeconds = configured.SimulatedSessionDurationSeconds,
            Storage = new AgentStorageOptions
            {
                BaseDirectory = string.IsNullOrWhiteSpace(configured.Storage.BaseDirectory)
                    ? savedDefaults.Storage.BaseDirectory
                    : configured.Storage.BaseDirectory
            },
            LumaBooth = new LumaBoothOptions
            {
                Mode = configured.LumaBooth.Mode,
                ApiBaseUrl = configured.LumaBooth.ApiBaseUrl,
                ApiPassword = configured.LumaBooth.ApiPassword,
                TriggerListenerUrl = configured.LumaBooth.TriggerListenerUrl,
                StartTimeoutSeconds = configured.LumaBooth.StartTimeoutSeconds
            },
            Display = new DisplayOptions
            {
                LumaBoothWindowTitle = configured.Display.LumaBoothWindowTitle,
                BoothUiWindowTitle = configured.Display.BoothUiWindowTitle,
                BoothUiBaseUrl = configured.Display.BoothUiBaseUrl,
                ChromeExecutablePath = configured.Display.ChromeExecutablePath,
                ChromeUserDataDir = string.IsNullOrWhiteSpace(configured.Display.ChromeUserDataDir)
                    ? savedDefaults.Display.ChromeUserDataDir
                    : configured.Display.ChromeUserDataDir,
                LaunchBoothUiOnStartup = configured.Display.LaunchBoothUiOnStartup,
                KioskMode = configured.Display.KioskMode
            }
        };
    }
}

public sealed class StaticAgentRuntimeOptionsProvider(PhotoBizAgentOptions options) : IAgentRuntimeOptionsProvider
{
    public Task<PhotoBizAgentOptions> LoadAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(options);
    }
}
