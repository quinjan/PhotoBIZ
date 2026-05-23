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
        Assert.Equal("configured-agent-secret", options.AgentCredential);
        Assert.Equal(workspace.RootDirectory, options.Storage.BaseDirectory);
        Assert.Equal(Path.Combine(workspace.RootDirectory, "chrome-kiosk"), options.Display.ChromeUserDataDir);
    }

    private static AgentConfigurationUpdate CreateUpdate(string? agentCredential, string? apiPassword)
    {
        return new AgentConfigurationUpdate(
            "http://localhost:5082",
            "sma-001",
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
