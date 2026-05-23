using PhotoBIZ.Api.Data;

namespace PhotoBIZ.Api;

public static class PhotoBizBoothAvailability
{
    public static readonly TimeSpan AgentOfflineAfter = TimeSpan.FromSeconds(60);

    public static bool HasFreshAgentHeartbeat(Booth booth, DateTimeOffset now)
    {
        return booth.LastHeartbeatAt.HasValue &&
            booth.LastHeartbeatAt.Value >= now.Subtract(AgentOfflineAfter);
    }

    public static string GetEffectiveState(Booth booth, DateTimeOffset now)
    {
        return booth.Status == StatusValues.Booth.Active && HasFreshAgentHeartbeat(booth, now)
            ? booth.CurrentState
            : StatusValues.Booth.Offline;
    }

    public static bool IsAgentOffline(Booth booth, DateTimeOffset now)
    {
        return GetEffectiveState(booth, now) == StatusValues.Booth.Offline;
    }

    public static void MarkAgentHeartbeat(Booth booth, DateTimeOffset now)
    {
        booth.LastHeartbeatAt = now;
        if (booth.Status != StatusValues.Booth.Active)
        {
            booth.CurrentState = StatusValues.Booth.Offline;
            return;
        }

        booth.CurrentState = booth.CurrentState == StatusValues.Booth.Offline
            ? StatusValues.Booth.Welcome
            : booth.CurrentState;
    }
}
