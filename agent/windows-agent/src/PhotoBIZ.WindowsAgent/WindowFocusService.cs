using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;

namespace PhotoBIZ.WindowsAgent;

public interface IWindowFocusService
{
    Task ShowLumaBoothAsync(CancellationToken cancellationToken);
    Task ShowBoothUiAsync(CancellationToken cancellationToken);
}

public sealed class WindowFocusService(
    IOptions<PhotoBizAgentOptions> options,
    ILogger<WindowFocusService> logger) : IWindowFocusService
{
    private const int Restore = 9;
    private static readonly Action<ILogger, string, string, Exception?> LogWindowMissing =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(2200, nameof(LogWindowMissing)),
            "Could not find {DisplayName} window containing title {WindowTitle}.");
    private static readonly Action<ILogger, string, Exception?> LogWindowFocusFailed =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(2201, nameof(LogWindowFocusFailed)),
            "Could not bring {DisplayName} window to the foreground.");

    public Task ShowLumaBoothAsync(CancellationToken cancellationToken)
    {
        BringWindowToFront(options.Value.Display.LumaBoothWindowTitle, "LumaBooth");
        return Task.CompletedTask;
    }

    public Task ShowBoothUiAsync(CancellationToken cancellationToken)
    {
        BringWindowToFront(options.Value.Display.BoothUiWindowTitle, "Booth UI");
        return Task.CompletedTask;
    }

    private void BringWindowToFront(string titleContains, string displayName)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(titleContains))
        {
            return;
        }

        var handle = FindWindowByTitle(titleContains);
        if (handle == IntPtr.Zero)
        {
            LogWindowMissing(logger, displayName, titleContains, null);
            return;
        }

        ShowWindow(handle, Restore);
        if (!SetForegroundWindow(handle))
        {
            LogWindowFocusFailed(logger, displayName, null);
        }
    }

    private static IntPtr FindWindowByTitle(string titleContains)
    {
        var foundHandle = IntPtr.Zero;
        EnumWindows((handle, _) =>
        {
            var builder = new StringBuilder(512);
            var textLength = GetWindowText(handle, builder, builder.Capacity);
            if (textLength > 0 && builder.ToString().Contains(titleContains, StringComparison.OrdinalIgnoreCase))
            {
                foundHandle = handle;
                return false;
            }

            return true;
        }, IntPtr.Zero);

        return foundHandle;
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    [SuppressMessage("Performance", "CA1838:Avoid StringBuilder parameters for P/Invokes", Justification = "Window title lookup is rare and bounded to focus handoff events.")]
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
