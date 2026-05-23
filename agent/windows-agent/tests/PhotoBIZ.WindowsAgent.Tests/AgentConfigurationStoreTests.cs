using System.Text;
using PhotoBIZ.WindowsAgent;

namespace PhotoBIZ.WindowsAgent.Tests;

public sealed class AgentConfigurationStoreTests
{
    [Fact]
    public async Task SaveMasksAndEncryptsSecretsThenLoadsRuntimeOptions()
    {
        using var workspace = TempAgentWorkspace.Create();
        var store = workspace.CreateStore();

        await store.SaveAsync(CreateUpdate("agent-secret", "luma-secret"), CancellationToken.None);

        var fileText = await File.ReadAllTextAsync(workspace.ConfigurationFilePath);
        var snapshot = await store.LoadSnapshotAsync(CancellationToken.None);
        var runtime = await store.LoadRuntimeOptionsAsync(CancellationToken.None);

        Assert.DoesNotContain("agent-secret", fileText, StringComparison.Ordinal);
        Assert.DoesNotContain("luma-secret", fileText, StringComparison.Ordinal);
        Assert.Contains("test:", fileText, StringComparison.Ordinal);
        Assert.Equal("SMA-001", snapshot.BoothCode);
        Assert.True(snapshot.HasAgentCredential);
        Assert.True(snapshot.LumaBooth.HasApiPassword);
        Assert.Equal(workspace.RootDirectory, snapshot.Storage.BaseDirectory);
        Assert.Equal("agent-secret", runtime.AgentCredential);
        Assert.Equal("luma-secret", runtime.LumaBooth.ApiPassword);
        Assert.Equal(LumaBoothIntegrationMode.Api, runtime.LumaBooth.Mode);
        Assert.Equal(workspace.RootDirectory, runtime.Storage.BaseDirectory);
    }

    [Fact]
    public async Task SaveWithNullSecretInputsPreservesExistingEncryptedSecrets()
    {
        using var workspace = TempAgentWorkspace.Create();
        var store = workspace.CreateStore();
        await store.SaveAsync(CreateUpdate("agent-secret", "luma-secret"), CancellationToken.None);
        var firstFileText = await File.ReadAllTextAsync(workspace.ConfigurationFilePath);

        await store.SaveAsync(CreateUpdate(agentCredential: null, apiPassword: null) with
        {
            ApiBaseUrl = "https://api.example.test"
        }, CancellationToken.None);

        var secondFileText = await File.ReadAllTextAsync(workspace.ConfigurationFilePath);
        var runtime = await store.LoadRuntimeOptionsAsync(CancellationToken.None);

        Assert.Contains("https://api.example.test", secondFileText, StringComparison.Ordinal);
        Assert.Contains(ExtractProtectedValue(firstFileText, "agentCredential"), secondFileText, StringComparison.Ordinal);
        Assert.Contains(ExtractProtectedValue(firstFileText, "apiPassword"), secondFileText, StringComparison.Ordinal);
        Assert.Equal("agent-secret", runtime.AgentCredential);
        Assert.Equal("luma-secret", runtime.LumaBooth.ApiPassword);
    }

    [Fact]
    public async Task SaveWithEmptySecretInputsClearsExistingSecrets()
    {
        using var workspace = TempAgentWorkspace.Create();
        var store = workspace.CreateStore();
        await store.SaveAsync(CreateUpdate("agent-secret", "luma-secret"), CancellationToken.None);

        await store.SaveAsync(CreateUpdate(agentCredential: string.Empty, apiPassword: string.Empty), CancellationToken.None);

        var snapshot = await store.LoadSnapshotAsync(CancellationToken.None);
        var runtime = await store.LoadRuntimeOptionsAsync(CancellationToken.None);

        Assert.False(snapshot.HasAgentCredential);
        Assert.False(snapshot.LumaBooth.HasApiPassword);
        Assert.Equal(string.Empty, runtime.AgentCredential);
        Assert.Equal(string.Empty, runtime.LumaBooth.ApiPassword);
    }

    [Fact]
    public async Task ClearDeletesLocalConfigurationFile()
    {
        using var workspace = TempAgentWorkspace.Create();
        var store = workspace.CreateStore();
        await store.SaveAsync(CreateUpdate("agent-secret", "luma-secret"), CancellationToken.None);

        await store.ClearAsync(CancellationToken.None);

        Assert.False(File.Exists(workspace.ConfigurationFilePath));
    }

    [Fact]
    public void ConfiguredBaseDirectoryOverridesProgramDataDefault()
    {
        var configuredPath = Path.Combine(Path.GetTempPath(), "PhotoBIZ", "Agent", "configured");

        var path = AgentDataPaths.ResolveRootDirectory(configuredPath);

        Assert.Equal(configuredPath, path);
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
                "api",
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

    private static string ExtractProtectedValue(string json, string propertyName)
    {
        var marker = $"\"{propertyName}\": \"";
        var start = json.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Expected {propertyName} in configuration JSON.");
        start += marker.Length;
        var end = json.IndexOf('"', start);
        Assert.True(end > start, $"Expected {propertyName} value in configuration JSON.");
        return json[start..end];
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
