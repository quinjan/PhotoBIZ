using System.Net;
using System.Net.Http.Json;

namespace PhotoBIZ.WindowsAgent;

public interface IPhotoBizAgentApiClient
{
    Task<AgentPairPayload> PairAsync(string boothCode, CancellationToken cancellationToken);
    Task<AgentPairPayload> PairAsync(string apiBaseUrl, string boothCode, string agentCredential, CancellationToken cancellationToken);
    Task HeartbeatAsync(AgentHeartbeatPayload heartbeat, CancellationToken cancellationToken);
    Task OfflineAsync(string boothCode, CancellationToken cancellationToken);
    Task<AgentBoothUiLaunchPayload> CreateBoothUiLaunchAsync(string boothCode, CancellationToken cancellationToken);
    Task<AgentCommandPayload?> GetNextCommandAsync(string boothCode, CancellationToken cancellationToken);
    Task MarkSessionStartedAsync(ActiveLumaBoothSession session, string lumaboothEventType, CancellationToken cancellationToken);
    Task MarkSessionCompletedAsync(ActiveLumaBoothSession session, string lumaboothEventType, CancellationToken cancellationToken);
    Task MarkSessionFailedAsync(ActiveLumaBoothSession session, string reason, string? lumaboothEventType, CancellationToken cancellationToken);
    Task MarkPrintCompletedAsync(ActiveLumaBoothSession session, CancellationToken cancellationToken);
    Task MarkPrintFailedAsync(ActiveLumaBoothSession session, string reason, CancellationToken cancellationToken);
}

public sealed class PhotoBizAgentApiClient(
    HttpClient httpClient,
    IAgentRuntimeOptionsProvider optionsProvider) : IPhotoBizAgentApiClient
{
    public async Task<AgentPairPayload> PairAsync(string boothCode, CancellationToken cancellationToken)
    {
        var settings = await optionsProvider.LoadAsync(cancellationToken);
        return await PairAsync(settings.ApiBaseUrl, boothCode, settings.AgentCredential, cancellationToken);
    }

    public async Task<AgentPairPayload> PairAsync(
        string apiBaseUrl,
        string boothCode,
        string agentCredential,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildApiUri(apiBaseUrl, "/api/agent/pair"))
        {
            Content = JsonContent.Create(new { boothCode })
        };
        AddAgentCredential(request, agentCredential);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        EnsureAgentSuccess(response);
        return await response.Content.ReadFromJsonAsync<AgentPairPayload>(cancellationToken)
            ?? throw new InvalidOperationException("PhotoBIZ agent pair response was empty.");
    }

    public async Task HeartbeatAsync(AgentHeartbeatPayload heartbeat, CancellationToken cancellationToken)
    {
        var settings = await optionsProvider.LoadAsync(cancellationToken);
        using var response = await PostAgentJsonAsync(settings.ApiBaseUrl, settings.AgentCredential, "/api/agent/heartbeat", heartbeat, cancellationToken);
        EnsureAgentSuccess(response);
    }

    public async Task OfflineAsync(string boothCode, CancellationToken cancellationToken)
    {
        var settings = await optionsProvider.LoadAsync(cancellationToken);
        using var response = await PostAgentJsonAsync(settings.ApiBaseUrl, settings.AgentCredential, "/api/agent/offline", new { boothCode }, cancellationToken);
        EnsureAgentSuccess(response);
    }

    public async Task<AgentBoothUiLaunchPayload> CreateBoothUiLaunchAsync(string boothCode, CancellationToken cancellationToken)
    {
        var settings = await optionsProvider.LoadAsync(cancellationToken);
        using var response = await PostAgentJsonAsync(
            settings.ApiBaseUrl,
            settings.AgentCredential,
            "/api/agent/booth-ui-launch",
            new { boothCode },
            cancellationToken);
        EnsureAgentSuccess(response);
        return await response.Content.ReadFromJsonAsync<AgentBoothUiLaunchPayload>(cancellationToken)
            ?? throw new InvalidOperationException("PhotoBIZ booth UI launch response was empty.");
    }

    public async Task<AgentCommandPayload?> GetNextCommandAsync(string boothCode, CancellationToken cancellationToken)
    {
        var settings = await optionsProvider.LoadAsync(cancellationToken);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            BuildApiUri(settings.ApiBaseUrl, $"/api/agent/commands/next?boothCode={Uri.EscapeDataString(boothCode)}"));
        AddAgentCredential(request, settings.AgentCredential);

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return null;
        }

        EnsureAgentSuccess(response);
        return await response.Content.ReadFromJsonAsync<AgentCommandPayload>(cancellationToken);
    }

    public Task MarkSessionStartedAsync(ActiveLumaBoothSession session, string lumaboothEventType, CancellationToken cancellationToken)
    {
        return PostSessionEventAsync(
            $"/api/agent/transactions/{session.TransactionId}/session-started",
            session,
            lumaboothEventType,
            reason: null,
            cancellationToken);
    }

    public Task MarkSessionCompletedAsync(ActiveLumaBoothSession session, string lumaboothEventType, CancellationToken cancellationToken)
    {
        return PostSessionEventAsync(
            $"/api/agent/transactions/{session.TransactionId}/session-completed",
            session,
            lumaboothEventType,
            reason: null,
            cancellationToken);
    }

    public Task MarkSessionFailedAsync(ActiveLumaBoothSession session, string reason, string? lumaboothEventType, CancellationToken cancellationToken)
    {
        return PostSessionEventAsync(
            $"/api/agent/transactions/{session.TransactionId}/session-failed",
            session,
            lumaboothEventType,
            reason,
            cancellationToken);
    }

    public Task MarkPrintCompletedAsync(ActiveLumaBoothSession session, CancellationToken cancellationToken)
    {
        return PostSessionEventAsync(
            $"/api/agent/transactions/{session.TransactionId}/print-completed",
            session,
            "print_completed",
            reason: null,
            cancellationToken);
    }

    public Task MarkPrintFailedAsync(ActiveLumaBoothSession session, string reason, CancellationToken cancellationToken)
    {
        return PostSessionEventAsync(
            $"/api/agent/transactions/{session.TransactionId}/print-failed",
            session,
            "print_failed",
            reason,
            cancellationToken);
    }

    private async Task PostSessionEventAsync(
        string path,
        ActiveLumaBoothSession session,
        string? lumaboothEventType,
        string? reason,
        CancellationToken cancellationToken)
    {
        var settings = await optionsProvider.LoadAsync(cancellationToken);
        using var response = await PostAgentJsonAsync(settings.ApiBaseUrl, settings.AgentCredential, path, new
        {
            boothCode = settings.BoothCode,
            lumaboothSessionRef = session.LumaboothSessionRef,
            lumaboothEventType,
            reason
        }, cancellationToken);

        EnsureAgentSuccess(response);
    }

    private async Task<HttpResponseMessage> PostAgentJsonAsync(
        string apiBaseUrl,
        string agentCredential,
        string path,
        object payload,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildApiUri(apiBaseUrl, path))
        {
            Content = JsonContent.Create(payload)
        };
        AddAgentCredential(request, agentCredential);
        return await httpClient.SendAsync(request, cancellationToken);
    }

    private static void AddAgentCredential(HttpRequestMessage request, string agentCredential)
    {
        if (!string.IsNullOrWhiteSpace(agentCredential))
        {
            request.Headers.Add("X-Agent-Credential", agentCredential);
        }
    }

    private static void EnsureAgentSuccess(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new AgentCredentialUnauthorizedException();
        }

        response.EnsureSuccessStatusCode();
    }

    private static Uri BuildApiUri(string apiBaseUrl, string path)
    {
        if (!Uri.TryCreate(apiBaseUrl.Trim(), UriKind.Absolute, out var baseUri))
        {
            throw new InvalidOperationException("PhotoBIZ API base URL must be an absolute URL.");
        }

        return new Uri(baseUri, path);
    }
}

public sealed record AgentPairPayload(Guid BoothId, string BoothName, string BoothCode);

public sealed record AgentHeartbeatPayload(
    string BoothCode,
    string AgentVersion,
    string RuntimeKind,
    bool KioskRunning,
    string LumaBoothMode,
    bool ApiReachable,
    bool ChromeLaunched,
    bool TriggerListenerRunning,
    bool? LumaBoothReachable);
