using System.Text;
using Microsoft.Extensions.Options;
using PhotoBIZ.WindowsAgent;

namespace PhotoBIZ.WindowsAgent.Tests;

public sealed class AgentRuntimeOptionsProviderTests
{
    [Fact]
    public async Task LoadUsesSavedEncryptedConfigurationWhenPaired()
    {
        using var workspace = TempAgentWorkspace.Create();
        var store = workspace.CreateStore();
        await store.SaveAsync(CreateUpdate("saved-agent-secret", "saved-luma-secret"), CancellationToken.None);
        var provider = new AgentRuntimeOptionsProvider(
            store,
            Options.Create(new PhotoBizAgentOptions
            {
                ApiBaseUrl = "http://configured.example.test",
                BoothCode = "DEV-001",
                AgentCredential = "configured-agent-secret"
            }));

        var options = await provider.LoadAsync(CancellationToken.None);

        Assert.Equal("http://localhost:5082", options.ApiBaseUrl);
        Assert.Equal("SMA-001", options.BoothCode);
        Assert.Equal("Small Mall Booth", options.BoothName);
        Assert.Equal("saved-agent-secret", options.AgentCredential);
        Assert.Equal("saved-luma-secret", options.LumaBooth.ApiPassword);
    }

    [Fact]
    public async Task LoadFallsBackToConfiguredDevelopmentOptionsWhenNoSavedPairingExists()
    {
        using var workspace = TempAgentWorkspace.Create();
        var store = workspace.CreateStore();
        var provider = new AgentRuntimeOptionsProvider(
            store,
            Options.Create(new PhotoBizAgentOptions
            {
                ApiBaseUrl = "http://localhost:5082",
                BoothCode = "DEV-001",
                BoothName = "Development Booth",
                AgentCredential = "configured-agent-secret",
                Display = new DisplayOptions
                {
                    BoothUiBaseUrl = "http://localhost:4201",
                    ChromeUserDataDir = string.Empty
                }
            }));

        var options = await provider.LoadAsync(CancellationToken.None);

        Assert.Equal("http://localhost:5082", options.ApiBaseUrl);
        Assert.Equal("DEV-001", options.BoothCode);
        Assert.Equal("Development Booth", options.BoothName);
        Assert.Equal("configured-agent-secret", options.AgentCredential);
        Assert.Equal(workspace.RootDirectory, options.Storage.BaseDirectory);
        Assert.Equal(Path.Combine(workspace.RootDirectory, "chrome-kiosk"), options.Display.ChromeUserDataDir);
    }

    [Fact]
    public async Task LoadKeepsSavedLocalSettingsWhenUsingConfiguredDevelopmentCredential()
    {
        using var workspace = TempAgentWorkspace.Create();
        var store = workspace.CreateStore();
        await store.SaveAsync(CreateUpdate(agentCredential: null, apiPassword: null) with
        {
            BoothCode = "dev-002",
            BoothName = "Saved Development Booth",
            Display = new DisplayConfigurationUpdate(
                "Saved Luma",
                "Saved Booth UI",
                "http://localhost:4301",
                @"C:\Chrome\chrome.exe",
                @"C:\PhotoBIZ\SavedChromeProfile",
                LaunchBoothUiOnStartup: true,
                KioskMode: true),
            LumaBooth = new LumaBoothConfigurationUpdate(
                LumaBoothIntegrationMode.Api,
                "http://localhost:1501",
                ApiPassword: null,
                "http://127.0.0.1:5618/lumabooth/events",
                StartTimeoutSeconds: 19)
        }, CancellationToken.None);
        var provider = new AgentRuntimeOptionsProvider(
            store,
            Options.Create(new PhotoBizAgentOptions
            {
                ApiBaseUrl = "http://localhost:5082",
                BoothCode = "DEV-001",
                BoothName = "Configured Development Booth",
                AgentCredential = "configured-agent-secret",
                LumaBooth = new LumaBoothOptions
                {
                    Mode = LumaBoothIntegrationMode.Simulator,
                    ApiBaseUrl = "http://localhost:1500",
                    ApiPassword = "configured-luma-secret",
                    TriggerListenerUrl = "http://127.0.0.1:5617/lumabooth/events",
                    StartTimeoutSeconds = 15
                },
                Display = new DisplayOptions
                {
                    BoothUiBaseUrl = "http://localhost:4201",
                    ChromeUserDataDir = string.Empty,
                    KioskMode = false
                }
            }));

        var options = await provider.LoadAsync(CancellationToken.None);

        Assert.Equal("DEV-002", options.BoothCode);
        Assert.Equal("Saved Development Booth", options.BoothName);
        Assert.Equal("configured-agent-secret", options.AgentCredential);
        Assert.Equal("http://localhost:4301", options.Display.BoothUiBaseUrl);
        Assert.Equal(@"C:\Chrome\chrome.exe", options.Display.ChromeExecutablePath);
        Assert.Equal(@"C:\PhotoBIZ\SavedChromeProfile", options.Display.ChromeUserDataDir);
        Assert.True(options.Display.KioskMode);
        Assert.Equal(LumaBoothIntegrationMode.Api, options.LumaBooth.Mode);
        Assert.Equal("http://localhost:1501", options.LumaBooth.ApiBaseUrl);
        Assert.Equal("configured-luma-secret", options.LumaBooth.ApiPassword);
        Assert.Equal("http://127.0.0.1:5618/lumabooth/events", options.LumaBooth.TriggerListenerUrl);
        Assert.Equal(19, options.LumaBooth.StartTimeoutSeconds);
    }

    private static AgentConfigurationUpdate CreateUpdate(string? agentCredential, string? apiPassword)
    {
        return new AgentConfigurationUpdate(
            "http://localhost:5082",
            "sma-001",
            "Small Mall Booth",
            agentCredential,
            PollIntervalSeconds: 5,
            SimulatedSessionDurationSeconds: 6,
            new LumaBoothConfigurationUpdate(
                LumaBoothIntegrationMode.Api,
                "http://localhost:1500",
                apiPassword,
                "http://127.0.0.1:5617/lumabooth/events",
                StartTimeoutSeconds: 15),
            new DisplayConfigurationUpdate(
                "dslrBooth",
                "BoothUi",
                "http://localhost:4201",
                string.Empty,
                @"C:\ProgramData\PhotoBIZ\Agent\chrome-kiosk",
                LaunchBoothUiOnStartup: true,
                KioskMode: false));
    }

    private sealed class TempAgentWorkspace : IDisposable
    {
        private TempAgentWorkspace(string rootDirectory)
        {
            RootDirectory = rootDirectory;
        }

        public string RootDirectory { get; }

        public static TempAgentWorkspace Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "PhotoBIZ-Agent-Tests", Guid.NewGuid().ToString("N"));
            return new TempAgentWorkspace(root);
        }

        public FileAgentConfigurationStore CreateStore()
        {
            return new FileAgentConfigurationStore(
                new TestAgentDataPaths(RootDirectory),
                new TestSecretProtector());
        }

        public void Dispose()
        {
            if (Directory.Exists(RootDirectory))
            {
                Directory.Delete(RootDirectory, recursive: true);
            }
        }
    }

    private sealed class TestAgentDataPaths(string rootDirectory) : IAgentDataPaths
    {
        public string RootDirectory { get; } = rootDirectory;
        public string ConfigurationFilePath => Path.Combine(RootDirectory, "config.json");
        public string ActiveSessionFilePath => Path.Combine(RootDirectory, "active-session.json");
        public string BoothUiLaunchStateFilePath => Path.Combine(RootDirectory, "booth-ui-launch.json");
    }

    private sealed class TestSecretProtector : IAgentSecretProtector
    {
        public string Protect(string secret)
        {
            return "test:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(secret));
        }

        public string Unprotect(string protectedSecret)
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(protectedSecret["test:".Length..]));
        }
    }
}
