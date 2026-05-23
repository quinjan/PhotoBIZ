using System.Security.Cryptography;
using System.Text;

namespace PhotoBIZ.WindowsAgent;

public interface IAgentSecretProtector
{
    string Protect(string secret);
    string Unprotect(string protectedSecret);
}

public sealed class WindowsDpapiAgentSecretProtector : IAgentSecretProtector
{
    private const string Prefix = "dpapi:";

    public string Protect(string secret)
    {
        if (string.IsNullOrEmpty(secret))
        {
            return string.Empty;
        }

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows DPAPI secret protection requires Windows.");
        }

        var bytes = Encoding.UTF8.GetBytes(secret);
        var protectedBytes = ProtectedData.Protect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        CryptographicOperations.ZeroMemory(bytes);
        return Prefix + Convert.ToBase64String(protectedBytes);
    }

    public string Unprotect(string protectedSecret)
    {
        if (string.IsNullOrEmpty(protectedSecret))
        {
            return string.Empty;
        }

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows DPAPI secret protection requires Windows.");
        }

        var value = protectedSecret.StartsWith(Prefix, StringComparison.Ordinal)
            ? protectedSecret[Prefix.Length..]
            : protectedSecret;
        var protectedBytes = Convert.FromBase64String(value);
        var bytes = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);

        try
        {
            return Encoding.UTF8.GetString(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }
}
