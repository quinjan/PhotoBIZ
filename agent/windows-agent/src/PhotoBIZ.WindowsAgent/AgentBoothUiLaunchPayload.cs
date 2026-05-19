namespace PhotoBIZ.WindowsAgent;

public sealed record AgentBoothUiLaunchPayload(Guid BoothId, string BoothCode, string KioskToken);
