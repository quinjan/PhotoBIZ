namespace PhotoBIZ.WindowsAgent;

public sealed class AgentCredentialUnauthorizedException : InvalidOperationException
{
    public AgentCredentialUnauthorizedException()
        : base("Agent credential was rejected. Re-pair this booth with a credential from Admin Web.")
    {
    }
}
