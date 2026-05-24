using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;

namespace PhotoBIZ.WindowsAgent;

public interface IAgentDiagnosticsExporter
{
    Task<AgentDiagnosticsExportResult> ExportAsync(CancellationToken cancellationToken);
}

public interface IAgentDiagnosticsSanitizer
{
    string Sanitize(string text, IEnumerable<string> secretValues);
}

public sealed record AgentDiagnosticsExportResult(string FilePath, string SanitizedText);

public sealed class AgentDiagnosticsExporter(
    IAgentConfigurationStore configurationStore,
    IAgentRuntimeOptionsProvider optionsProvider,
    IAgentDataPaths dataPaths,
    IAgentDiagnosticsSanitizer sanitizer) : IAgentDiagnosticsExporter
{
    public async Task<AgentDiagnosticsExportResult> ExportAsync(CancellationToken cancellationToken)
    {
        var snapshot = await configurationStore.LoadSnapshotAsync(cancellationToken);
        var runtimeOptions = await optionsProvider.LoadAsync(cancellationToken);
        var diagnostics = BuildDiagnostics(snapshot, runtimeOptions);
        var sanitized = sanitizer.Sanitize(
            diagnostics,
            [
                runtimeOptions.AgentCredential,
                runtimeOptions.LumaBooth.ApiPassword
            ]);

        var diagnosticsDirectory = Path.Combine(dataPaths.RootDirectory, "diagnostics");
        Directory.CreateDirectory(diagnosticsDirectory);

        var filePath = Path.Combine(
            diagnosticsDirectory,
            $"photobiz-agent-diagnostics-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.txt");

        await File.WriteAllTextAsync(filePath, sanitized, Encoding.UTF8, cancellationToken);
        return new AgentDiagnosticsExportResult(filePath, sanitized);
    }

    private string BuildDiagnostics(AgentConfigurationSnapshot snapshot, PhotoBizAgentOptions runtimeOptions)
    {
        var builder = new StringBuilder();
        builder.Append("PhotoBIZ Agent Diagnostics").AppendLine();
        builder.Append("Version: ").AppendLine(AgentMetadata.Version);
        builder.Append("GeneratedUtc: ").AppendLine(DateTimeOffset.UtcNow.ToString("O"));
        builder.Append("RootDirectory: ").AppendLine(dataPaths.RootDirectory);
        builder.Append("ConfigurationFile: ").AppendLine(dataPaths.ConfigurationFilePath);
        builder.Append("ConfigurationExists: ").AppendLine(File.Exists(dataPaths.ConfigurationFilePath).ToString());
        builder.Append("ActiveSessionExists: ").AppendLine(File.Exists(dataPaths.ActiveSessionFilePath).ToString());
        builder.Append("API: ").AppendLine(runtimeOptions.ApiBaseUrl);
        builder.Append("BoothCode: ").AppendLine(runtimeOptions.BoothCode);
        builder.Append("BoothName: ").AppendLine(runtimeOptions.BoothName);
        builder.Append("StoredBoothName: ").AppendLine(snapshot.BoothName);
        builder.Append("Pairing: ").AppendLine(snapshot.HasAgentCredential ? "Paired" : "Not paired");
        builder.Append("AgentCredential: ").AppendLine(snapshot.HasAgentCredential ? "[stored]" : "[not stored]");
        builder.Append("PollIntervalSeconds: ").AppendLine(runtimeOptions.PollIntervalSeconds.ToString(CultureInfo.InvariantCulture));
        builder.Append("SimulatedSessionDurationSeconds: ").AppendLine(runtimeOptions.SimulatedSessionDurationSeconds.ToString(CultureInfo.InvariantCulture));
        builder.Append("LumaBoothMode: ").AppendLine(runtimeOptions.LumaBooth.Mode);
        builder.Append("LumaBoothApiBaseUrl: ").AppendLine(runtimeOptions.LumaBooth.ApiBaseUrl);
        builder.Append("LumaBoothApiPassword: ").AppendLine(snapshot.LumaBooth.HasApiPassword ? "[stored]" : "[not stored]");
        builder.Append("LumaBoothTriggerListenerUrl: ").AppendLine(runtimeOptions.LumaBooth.TriggerListenerUrl);
        builder.Append("LumaBoothStartTimeoutSeconds: ").AppendLine(runtimeOptions.LumaBooth.StartTimeoutSeconds.ToString(CultureInfo.InvariantCulture));
        builder.Append("BoothUiBaseUrl: ").AppendLine(runtimeOptions.Display.BoothUiBaseUrl);
        builder.Append("ChromeExecutablePath: ").AppendLine(runtimeOptions.Display.ChromeExecutablePath);
        builder.Append("ChromeUserDataDir: ").AppendLine(runtimeOptions.Display.ChromeUserDataDir);
        builder.Append("LaunchBoothUiOnStartup: ").AppendLine(runtimeOptions.Display.LaunchBoothUiOnStartup.ToString());
        builder.Append("KioskMode: ").AppendLine(runtimeOptions.Display.KioskMode.ToString());
        return builder.ToString();
    }
}

public sealed partial class AgentDiagnosticsSanitizer : IAgentDiagnosticsSanitizer
{
    public string Sanitize(string text, IEnumerable<string> secretValues)
    {
        var sanitized = text;

        foreach (var secret in secretValues.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal))
        {
            sanitized = sanitized.Replace(secret, "[REDACTED]", StringComparison.Ordinal);
        }

        sanitized = PasswordQueryStringRegex().Replace(sanitized, "$1[REDACTED]");
        sanitized = SecretHeaderRegex().Replace(sanitized, "$1[REDACTED]");
        sanitized = BoothUiPathTokenRegex().Replace(sanitized, "$1[REDACTED]");
        return sanitized;
    }

    [GeneratedRegex(@"(?i)(password=)[^&\s]+")]
    private static partial Regex PasswordQueryStringRegex();

    [GeneratedRegex(@"(?i)(X-Agent-Credential\s*[:=]\s*)[^\s,;]+")]
    private static partial Regex SecretHeaderRegex();

    [GeneratedRegex(@"(https?://[^\s/]+/)[A-Za-z0-9._~%+-]{20,}")]
    private static partial Regex BoothUiPathTokenRegex();
}
