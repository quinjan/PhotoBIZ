namespace PhotoBIZ.WindowsAgent;

public sealed record AgentCommandPayload(
    Guid TransactionId,
    string TransactionNumber,
    string Command,
    string LumaboothSessionMode,
    string OfferType,
    string TransactionType,
    string IncludedPrintEntitlement,
    int ExtraPrintCount);

public sealed record ActiveLumaBoothSession(
    Guid TransactionId,
    string TransactionNumber,
    string Command,
    string LumaboothSessionMode,
    string LumaboothSessionRef,
    DateTimeOffset StartedAt);

public sealed record LumaBoothTriggerEvent(string EventType, string? Param1, string? Param2, string? Param3, string? Param4);

public static class LumaBoothSessionModes
{
    public const string Print = "PRINT";
    public const string Gif = "GIF";
    public const string Boomerang = "BOOMERANG";
    public const string Video = "VIDEO";
    public const string LegacySessionStandard = "SESSION_STANDARD";

    public static string Normalize(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        return normalized switch
        {
            Print or LegacySessionStandard => Print,
            Gif => Gif,
            Boomerang => Boomerang,
            Video => Video,
            _ => Print
        };
    }

    public static string ToApiMode(string value)
    {
        return Normalize(value).ToLowerInvariant();
    }
}
