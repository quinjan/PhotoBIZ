namespace PhotoBIZ.WindowsAgent;

public sealed class LumaBoothTriggerHandler(
    IActiveLumaBoothSessionStore sessionStore,
    IPhotoBizAgentApiClient photoBizApi,
    IWindowFocusService windowFocusService,
    ILogger<LumaBoothTriggerHandler> logger)
{
    private static readonly Action<ILogger, string, Exception?> LogMissingContext =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(2000, nameof(LogMissingContext)),
            "Ignoring LumaBooth trigger {EventType} because no PhotoBIZ session is active.");
    private static readonly Action<ILogger, string, string, Exception?> LogKnownTrigger =
        LoggerMessage.Define<string, string>(
            LogLevel.Information,
            new EventId(2001, nameof(LogKnownTrigger)),
            "Received LumaBooth trigger {EventType} for transaction {TransactionNumber}.");
    private static readonly Action<ILogger, string, string, Exception?> LogUnsupportedTrigger =
        LoggerMessage.Define<string, string>(
            LogLevel.Information,
            new EventId(2002, nameof(LogUnsupportedTrigger)),
            "Ignoring unsupported LumaBooth trigger {EventType} for transaction {TransactionNumber}.");

    public async Task HandleAsync(LumaBoothTriggerEvent triggerEvent, CancellationToken cancellationToken)
    {
        var session = await sessionStore.LoadAsync(cancellationToken);
        if (session is null)
        {
            LogMissingContext(logger, triggerEvent.EventType, null);
            return;
        }

        switch (triggerEvent.EventType)
        {
            case "session_start":
                await photoBizApi.MarkSessionStartedAsync(session, triggerEvent.EventType, cancellationToken);
                break;
            case "session_end":
                await photoBizApi.MarkSessionCompletedAsync(session, triggerEvent.EventType, cancellationToken);
                await sessionStore.ClearAsync(cancellationToken);
                await windowFocusService.ShowBoothUiAsync(cancellationToken);
                break;
            case "printing":
            case "file_upload":
            case "processing_start":
            case "sharing_screen":
                LogKnownTrigger(logger, triggerEvent.EventType, session.TransactionNumber, null);
                break;
            default:
                LogUnsupportedTrigger(logger, triggerEvent.EventType, session.TransactionNumber, null);
                break;
        }
    }
}
