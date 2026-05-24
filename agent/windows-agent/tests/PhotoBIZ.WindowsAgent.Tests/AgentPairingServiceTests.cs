using System.Net;
using System.Text;
using PhotoBIZ.WindowsAgent;

namespace PhotoBIZ.WindowsAgent.Tests;

public sealed class AgentPairingServiceTests
{
    [Fact]
    public async Task PairValidatesCredentialBeforeSavingEncryptedConfiguration()
    {
        using var workspace = TempAgentWorkspace.Create();
        var store = workspace.CreateStore();
        var api = new RecordingPhotoBizAgentApiClient(
            new AgentPairPayload(Guid.NewGuid(), "Small Mall Booth", "SMA-001"));
        var service = CreateService(api, store);

        var result = await service.PairAsync(
            new AgentPairingRequest(" http://localhost:5082/ ", " sma-001 ", " agent-secret "),
            CancellationToken.None);

        var fileText = await File.ReadAllTextAsync(workspace.ConfigurationFilePath);
        var runtime = await store.LoadRuntimeOptionsAsync(CancellationToken.None);

        Assert.Equal("http://localhost:5082/", api.PairApiBaseUrl);
        Assert.Equal("SMA-001", api.PairBoothCode);
        Assert.Equal("agent-secret", api.PairAgentCredential);
        Assert.Equal("SMA-001", result.BoothCode);
        Assert.Equal("Small Mall Booth", result.BoothName);
        Assert.True(result.Configuration.HasAgentCredential);
        Assert.DoesNotContain("agent-secret", fileText, StringComparison.Ordinal);
        Assert.Equal("agent-secret", runtime.AgentCredential);
        Assert.Equal("SMA-001", runtime.BoothCode);
        Assert.Equal("Small Mall Booth", runtime.BoothName);
    }

    [Fact]
    public async Task PairDoesNotReplaceExistingConfigurationWhenValidationFails()
    {
        using var workspace = TempAgentWorkspace.Create();
        var store = workspace.CreateStore();
        await store.SaveAsync(CreateUpdate("old-agent-secret", "luma-secret"), CancellationToken.None);
        var api = new RecordingPhotoBizAgentApiClient(
            new AgentPairPayload(Guid.NewGuid(), "Small Mall Booth", "SMA-001"))
        {
            PairFailure = new HttpRequestException("Unauthorized", null, HttpStatusCode.Unauthorized)
        };
        var service = CreateService(api, store);

        await Assert.ThrowsAsync<HttpRequestException>(() => service.PairAsync(
            new AgentPairingRequest("http://localhost:5082", "SMA-001", "bad-secret"),
            CancellationToken.None));

        var runtime = await store.LoadRuntimeOptionsAsync(CancellationToken.None);
        Assert.Equal("old-agent-secret", runtime.AgentCredential);
        Assert.Equal("luma-secret", runtime.LumaBooth.ApiPassword);
    }

    [Fact]
    public async Task RePairStopsRuntimeAndClearsLocalStateBeforeSavingNewCredential()
    {
        using var workspace = TempAgentWorkspace.Create();
        var store = workspace.CreateStore();
        await store.SaveAsync(CreateUpdate("old-agent-secret", "luma-secret"), CancellationToken.None);
        var api = new RecordingPhotoBizAgentApiClient(
            new AgentPairPayload(Guid.NewGuid(), "New Booth", "NEW-002"));
        var runtime = new RecordingAgentBoothRuntime { IsRunning = true };
        var sessionStore = new RecordingActiveSessionStore();
        var launcher = new RecordingBoothUiLauncher();
        var service = CreateService(api, store, runtime, sessionStore, launcher);

        var result = await service.RePairAsync(
            new AgentPairingRequest("http://localhost:5082", "new-002", "new-agent-secret"),
            CancellationToken.None);

        var runtimeOptions = await store.LoadRuntimeOptionsAsync(CancellationToken.None);
        Assert.Equal(1, runtime.StopCalls);
        Assert.Equal(1, sessionStore.ClearCalls);
        Assert.Equal(1, launcher.CloseCalls);
        Assert.False(runtime.IsRunning);
        Assert.Equal("NEW-002", result.BoothCode);
        Assert.Equal("new-agent-secret", runtimeOptions.AgentCredential);
        Assert.Equal("New Booth", runtimeOptions.BoothName);
        Assert.Equal("luma-secret", runtimeOptions.LumaBooth.ApiPassword);
    }

    [Fact]
    public async Task RePairPreservesNonSecretSettings()
    {
        using var workspace = TempAgentWorkspace.Create();
        var store = workspace.CreateStore();
        await store.SaveAsync(CreateUpdate("old-agent-secret", "luma-secret") with
        {
            PollIntervalSeconds = 11,
            SimulatedSessionDurationSeconds = 12,
            Display = new DisplayConfigurationUpdate(
                "Luma Title",
                "Booth Title",
                "http://localhost:4300",
                @"C:\Chrome\chrome.exe",
                @"C:\PhotoBIZ\ChromeProfile",
                LaunchBoothUiOnStartup: false,
                KioskMode: false)
        }, CancellationToken.None);
        var api = new RecordingPhotoBizAgentApiClient(
            new AgentPairPayload(Guid.NewGuid(), "New Booth", "NEW-002"));
        var service = CreateService(api, store);

        await service.RePairAsync(
            new AgentPairingRequest("https://api.example.test", "new-002", "new-agent-secret"),
            CancellationToken.None);

        var runtime = await store.LoadRuntimeOptionsAsync(CancellationToken.None);
        Assert.Equal("https://api.example.test", runtime.ApiBaseUrl);
        Assert.Equal("NEW-002", runtime.BoothCode);
        Assert.Equal("New Booth", runtime.BoothName);
        Assert.Equal(11, runtime.PollIntervalSeconds);
        Assert.Equal(12, runtime.SimulatedSessionDurationSeconds);
        Assert.Equal("luma-secret", runtime.LumaBooth.ApiPassword);
        Assert.Equal("http://localhost:4300", runtime.Display.BoothUiBaseUrl);
        Assert.Equal(@"C:\Chrome\chrome.exe", runtime.Display.ChromeExecutablePath);
        Assert.False(runtime.Display.LaunchBoothUiOnStartup);
        Assert.False(runtime.Display.KioskMode);
    }

    private static AgentPairingService CreateService(
        IPhotoBizAgentApiClient api,
        IAgentConfigurationStore store,
        IAgentBoothRuntime? runtime = null,
        IActiveLumaBoothSessionStore? sessionStore = null,
        IBoothUiLauncher? launcher = null)
    {
        return new AgentPairingService(
            api,
            store,
            runtime ?? new RecordingAgentBoothRuntime(),
            sessionStore ?? new RecordingActiveSessionStore(),
            launcher ?? new RecordingBoothUiLauncher());
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

    private sealed class RecordingPhotoBizAgentApiClient(AgentPairPayload pairResponse) : IPhotoBizAgentApiClient
    {
        public string? PairApiBaseUrl { get; private set; }
        public string? PairBoothCode { get; private set; }
        public string? PairAgentCredential { get; private set; }
        public Exception? PairFailure { get; init; }

        public Task<AgentPairPayload> PairAsync(string boothCode, CancellationToken cancellationToken)
        {
            return Task.FromResult(pairResponse);
        }

        public Task<AgentPairPayload> PairAsync(
            string apiBaseUrl,
            string boothCode,
            string agentCredential,
            CancellationToken cancellationToken)
        {
            PairApiBaseUrl = apiBaseUrl;
            PairBoothCode = boothCode;
            PairAgentCredential = agentCredential;
            return PairFailure is not null
                ? Task.FromException<AgentPairPayload>(PairFailure)
                : Task.FromResult(pairResponse);
        }

        public Task HeartbeatAsync(AgentHeartbeatPayload heartbeat, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task OfflineAsync(string boothCode, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<AgentBoothUiLaunchPayload> CreateBoothUiLaunchAsync(string boothCode, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AgentBoothUiLaunchPayload(Guid.NewGuid(), boothCode, "kiosk-token"));
        }

        public Task<AgentCommandPayload?> GetNextCommandAsync(string boothCode, CancellationToken cancellationToken)
        {
            return Task.FromResult<AgentCommandPayload?>(null);
        }

        public Task MarkSessionStartedAsync(ActiveLumaBoothSession session, string lumaboothEventType, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task MarkSessionCompletedAsync(ActiveLumaBoothSession session, string lumaboothEventType, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task MarkSessionFailedAsync(ActiveLumaBoothSession session, string reason, string? lumaboothEventType, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task MarkPrintCompletedAsync(ActiveLumaBoothSession session, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task MarkPrintFailedAsync(ActiveLumaBoothSession session, string reason, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingAgentBoothRuntime : IAgentBoothRuntime
    {
        public bool IsRunning { get; set; }
        public int StopCalls { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            IsRunning = true;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            StopCalls++;
            IsRunning = false;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingActiveSessionStore : IActiveLumaBoothSessionStore
    {
        public int ClearCalls { get; private set; }

        public Task<ActiveLumaBoothSession?> LoadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<ActiveLumaBoothSession?>(null);
        }

        public Task SaveAsync(ActiveLumaBoothSession session, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            ClearCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingBoothUiLauncher : IBoothUiLauncher
    {
        public int CloseCalls { get; private set; }
        public bool IsLaunchedProcessRunning => false;

        public Task<BoothUiLaunchResult> LaunchAsync(AgentBoothUiLaunchPayload launch, CancellationToken cancellationToken)
        {
            return Task.FromResult(new BoothUiLaunchResult(1234, $"http://localhost:4201/{launch.KioskToken}"));
        }

        public Task CloseLaunchedAsync(CancellationToken cancellationToken)
        {
            CloseCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class TempAgentWorkspace : IDisposable
    {
        private TempAgentWorkspace(string rootDirectory)
        {
            RootDirectory = rootDirectory;
        }

        public string RootDirectory { get; }
        public string ConfigurationFilePath => Path.Combine(RootDirectory, "config.json");

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
