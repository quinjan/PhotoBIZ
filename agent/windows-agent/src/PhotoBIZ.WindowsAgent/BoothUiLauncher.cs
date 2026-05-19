using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace PhotoBIZ.WindowsAgent;

public interface IBoothUiLauncher
{
    Task LaunchAsync(AgentBoothUiLaunchPayload launch, CancellationToken cancellationToken);
}

public sealed class ChromeBoothUiLauncher(
    IOptions<PhotoBizAgentOptions> options,
    ILogger<ChromeBoothUiLauncher> logger) : IBoothUiLauncher
{
    private static readonly Action<ILogger, string, Exception?> LogBoothUiLaunched =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(3000, nameof(LogBoothUiLaunched)),
            "Launched Booth UI browser for booth {BoothCode}.");

    private readonly DisplayOptions displayOptions = options.Value.Display;

    public Task LaunchAsync(AgentBoothUiLaunchPayload launch, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var url = BuildBoothUiUrl(displayOptions.BoothUiBaseUrl, launch.KioskToken);
        var chromePath = ResolveChromePath(displayOptions.ChromeExecutablePath);
        var userDataDir = ResolveChromeUserDataDir(displayOptions.ChromeUserDataDir);
        if (displayOptions.KioskMode)
        {
            Directory.CreateDirectory(userDataDir);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = chromePath,
            Arguments = BuildChromeArguments(url, displayOptions.KioskMode, userDataDir),
            UseShellExecute = false
        });

        LogBoothUiLaunched(logger, launch.BoothCode, null);
        return Task.CompletedTask;
    }

    public static string BuildBoothUiUrl(string boothUiBaseUrl, string kioskToken)
    {
        return $"{boothUiBaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(kioskToken)}";
    }

    public static string BuildChromeArguments(string url, bool kioskMode, string chromeUserDataDir)
    {
        if (!kioskMode)
        {
            return $"--new-window \"{url}\"";
        }

        return string.Join(
            ' ',
            "--kiosk",
            "--start-fullscreen",
            "--new-window",
            "--no-first-run",
            "--disable-translate",
            "--disable-session-crashed-bubble",
            "--disable-infobars",
            "--autoplay-policy=no-user-gesture-required",
            $"--user-data-dir=\"{chromeUserDataDir}\"",
            $"\"{url}\"");
    }

    private static string ResolveChromeUserDataDir(string configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return configuredPath;
        }

        var commonApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return string.IsNullOrWhiteSpace(commonApplicationData)
            ? Path.Combine(Path.GetTempPath(), "PhotoBIZ", "chrome-kiosk")
            : Path.Combine(commonApplicationData, "PhotoBIZ", "chrome-kiosk");
    }

    private static string ResolveChromePath(string configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return configuredPath;
        }

        foreach (var candidate in GetChromeCandidates())
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return "chrome.exe";
    }

    private static IEnumerable<string> GetChromeCandidates()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            yield return Path.Combine(programFiles, "Google", "Chrome", "Application", "chrome.exe");
        }

        if (!string.IsNullOrWhiteSpace(programFilesX86))
        {
            yield return Path.Combine(programFilesX86, "Google", "Chrome", "Application", "chrome.exe");
        }

        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            yield return Path.Combine(localAppData, "Google", "Chrome", "Application", "chrome.exe");
        }
    }
}
