namespace PhotoBIZ.WindowsAgent;

public interface IAgentPairingService
{
    Task<AgentPairingResult> PairAsync(AgentPairingRequest request, CancellationToken cancellationToken);
    Task<AgentPairingResult> RePairAsync(AgentPairingRequest request, CancellationToken cancellationToken);
}

public sealed record AgentPairingRequest(string ApiBaseUrl, string BoothCode, string AgentCredential);

public sealed record AgentPairingResult(
    Guid BoothId,
    string BoothName,
    string BoothCode,
    AgentConfigurationSnapshot Configuration);

public sealed class AgentPairingService(
    IPhotoBizAgentApiClient photoBizApi,
    IAgentConfigurationStore configurationStore,
    IAgentBoothRuntime runtime,
    IActiveLumaBoothSessionStore activeSessionStore,
    IBoothUiLauncher boothUiLauncher) : IAgentPairingService
{
    public Task<AgentPairingResult> PairAsync(AgentPairingRequest request, CancellationToken cancellationToken)
    {
        return PairAndSaveAsync(request, cancellationToken);
    }

    public async Task<AgentPairingResult> RePairAsync(AgentPairingRequest request, CancellationToken cancellationToken)
    {
        if (runtime.IsRunning)
        {
            await runtime.StopAsync(cancellationToken);
        }

        await activeSessionStore.ClearAsync(cancellationToken);
        await boothUiLauncher.CloseLaunchedAsync(cancellationToken);

        return await PairAndSaveAsync(request, cancellationToken);
    }

    private async Task<AgentPairingResult> PairAndSaveAsync(
        AgentPairingRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeRequest(request);
        var current = await configurationStore.LoadSnapshotAsync(cancellationToken);
        var pair = await photoBizApi.PairAsync(
            normalized.ApiBaseUrl,
            normalized.BoothCode,
            normalized.AgentCredential,
            cancellationToken);

        await configurationStore.SaveAsync(ToConfigurationUpdate(current, normalized, pair), cancellationToken);
        var saved = await configurationStore.LoadSnapshotAsync(cancellationToken);

        return new AgentPairingResult(pair.BoothId, pair.BoothName, pair.BoothCode, saved);
    }

    private static AgentConfigurationUpdate ToConfigurationUpdate(
        AgentConfigurationSnapshot current,
        NormalizedAgentPairingRequest request,
        AgentPairPayload pair)
    {
        return new AgentConfigurationUpdate(
            request.ApiBaseUrl,
            pair.BoothCode,
            pair.BoothName,
            request.AgentCredential,
            current.PollIntervalSeconds,
            current.SimulatedSessionDurationSeconds,
            new LumaBoothConfigurationUpdate(
                current.LumaBooth.Mode,
                current.LumaBooth.ApiBaseUrl,
                ApiPassword: null,
                current.LumaBooth.TriggerListenerUrl,
                current.LumaBooth.StartTimeoutSeconds),
            new DisplayConfigurationUpdate(
                current.Display.LumaBoothWindowTitle,
                current.Display.BoothUiWindowTitle,
                current.Display.BoothUiBaseUrl,
                current.Display.ChromeExecutablePath,
                current.Display.ChromeUserDataDir,
                current.Display.LaunchBoothUiOnStartup,
                current.Display.KioskMode));
    }

    private static NormalizedAgentPairingRequest NormalizeRequest(AgentPairingRequest request)
    {
        var apiBaseUrl = NormalizeRequired(request.ApiBaseUrl, "PhotoBIZ API base URL");
        if (!Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("PhotoBIZ API base URL must be an absolute HTTP or HTTPS URL.");
        }

        return new NormalizedAgentPairingRequest(
            apiBaseUrl,
            NormalizeRequired(request.BoothCode, "booth code").ToUpperInvariant(),
            NormalizeRequired(request.AgentCredential, "Agent credential"));
    }

    private static string NormalizeRequired(string value, string fieldName)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException($"{fieldName} is required.");
        }

        return normalized;
    }

    private sealed record NormalizedAgentPairingRequest(
        string ApiBaseUrl,
        string BoothCode,
        string AgentCredential);
}
