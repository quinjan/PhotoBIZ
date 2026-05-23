using System.Diagnostics;

namespace PhotoBIZ.WindowsAgent;

public interface IBoothUiLauncher
{
    bool IsLaunchedProcessRunning { get; }
    Task<BoothUiLaunchResult> LaunchAsync(AgentBoothUiLaunchPayload launch, CancellationToken cancellationToken);
    Task CloseLaunchedAsync(CancellationToken cancellationToken);
}

public sealed record BoothUiLaunchResult(int ProcessId, string Url);

public sealed class ChromeBoothUiLauncher(
    IAgentRuntimeOptionsProvider optionsProvider,
    ILogger<ChromeBoothUiLauncher> logger) : IBoothUiLauncher
{
    private static readonly Action<ILogger, string, Exception?> LogBoothUiLaunched =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(3000, nameof(LogBoothUiLaunched)),
            "Launched Booth UI browser for booth {BoothCode}.");
    private static readonly Action<ILogger, int, Exception?> LogBoothUiClosed =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(3001, nameof(LogBoothUiClosed)),
            "Closed PhotoBIZ-launched Booth UI browser process {ProcessId}.");

    private Process? launchedProcess;

    public bool IsLaunchedProcessRunning
    {
        get
        {
            if (launchedProcess is null)
            {
                return false;
            }

            try
            {
                launchedProcess.Refresh();
                return !launchedProcess.HasExited;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }

    public async Task<BoothUiLaunchResult> LaunchAsync(AgentBoothUiLaunchPayload launch, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await CloseLaunchedAsync(cancellationToken);

        var displayOptions = (await optionsProvider.LoadAsync(cancellationToken)).Display;
        var url = BuildBoothUiUrl(displayOptions.BoothUiBaseUrl, launch.KioskToken);
        var chromePath = ResolveChromePath(displayOptions.ChromeExecutablePath);
        var userDataDir = ResolveChromeUserDataDir(displayOptions.ChromeUserDataDir);
        if (displayOptions.KioskMode)
        {
            Directory.CreateDirectory(userDataDir);
        }

        launchedProcess = Process.Start(new ProcessStartInfo
        {
            FileName = chromePath,
            Arguments = BuildChromeArguments(url, displayOptions.KioskMode, userDataDir),
            UseShellExecute = false
        }) ?? throw new InvalidOperationException("Chrome process did not start.");

        LogBoothUiLaunched(logger, launch.BoothCode, null);
        return new BoothUiLaunchResult(launchedProcess.Id, url);
    }

    public async Task CloseLaunchedAsync(CancellationToken cancellationToken)
    {
        var process = launchedProcess;
        launchedProcess = null;

        if (process is null)
        {
            return;
        }

        try
        {
            process.Refresh();
            if (process.HasExited)
            {
                return;
            }

            _ = process.CloseMainWindow();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));

            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(cancellationToken);
            }

            LogBoothUiClosed(logger, process.Id, null);
        }
        finally
        {
            process.Dispose();
        }
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
