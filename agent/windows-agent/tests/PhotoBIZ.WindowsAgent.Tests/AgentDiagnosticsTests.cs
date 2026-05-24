using System.Text;
using Microsoft.Extensions.Options;
using PhotoBIZ.WindowsAgent;

namespace PhotoBIZ.WindowsAgent.Tests;

public sealed class AgentDiagnosticsTests
{
    [Fact]
    public void SanitizerRedactsKnownSecretsHeadersPasswordsAndKioskTokens()
    {
        var sanitizer = new AgentDiagnosticsSanitizer();
        const string raw =
            "X-Agent-Credential: agent-secret " +
            "http://localhost:4201/abcdefghijklmnopqrstuvwxyz123456 " +
            "http://localhost:1500/api/start?mode=print&password=luma-secret " +
            "literal luma-secret";

        var sanitized = sanitizer.Sanitize(raw, ["agent-secret", "luma-secret"]);

        Assert.DoesNotContain("agent-secret", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("luma-secret", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("abcdefghijklmnopqrstuvwxyz123456", sanitized, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", sanitized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExporterWritesSanitizedDiagnosticsFile()
    {
        using var workspace = TempAgentWorkspace.Create();
        var store = workspace.CreateStore();
        await store.SaveAsync(CreateUpdate("agent-secret", "luma-secret"), CancellationToken.None);
        var exporter = new AgentDiagnosticsExporter(
            store,
            new AgentRuntimeOptionsProvider(
                store,
                Options.Create(new PhotoBizAgentOptions())),
            new TestAgentDataPaths(workspace.RootDirectory),
            new AgentDiagnosticsSanitizer());

        var result = await exporter.ExportAsync(CancellationToken.None);

        Assert.True(File.Exists(result.FilePath));
        Assert.StartsWith(Path.Combine(workspace.RootDirectory, "diagnostics"), result.FilePath, StringComparison.Ordinal);
        Assert.DoesNotContain("agent-secret", result.SanitizedText, StringComparison.Ordinal);
        Assert.DoesNotContain("luma-secret", result.SanitizedText, StringComparison.Ordinal);
        Assert.Contains("Small Mall Booth", result.SanitizedText, StringComparison.Ordinal);
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
