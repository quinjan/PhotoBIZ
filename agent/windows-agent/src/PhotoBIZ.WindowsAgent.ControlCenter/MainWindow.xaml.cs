using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Forms = System.Windows.Forms;

namespace PhotoBIZ.WindowsAgent.ControlCenter;

public partial class MainWindow : Window, IDisposable
{
    private readonly AgentControlCenterViewModel viewModel;
    private readonly Forms.NotifyIcon notifyIcon;
    private bool allowClose;
    private bool loaded;

    public MainWindow(AgentControlCenterViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        notifyIcon = CreateNotifyIcon();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (loaded)
        {
            return;
        }

        loaded = true;
        await viewModel.InitializeAsync();
    }

    private void OnAgentCredentialChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox)
        {
            viewModel.AgentCredential = passwordBox.Password;
        }
    }

    private void OnLumaBoothApiPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox)
        {
            viewModel.LumaBoothApiPassword = passwordBox.Password;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AgentControlCenterViewModel.AgentCredential) &&
            string.IsNullOrEmpty(viewModel.AgentCredential) &&
            !string.IsNullOrEmpty(AgentCredentialBox.Password))
        {
            AgentCredentialBox.Password = string.Empty;
        }

        if (e.PropertyName == nameof(AgentControlCenterViewModel.LumaBoothApiPassword) &&
            string.IsNullOrEmpty(viewModel.LumaBoothApiPassword) &&
            !string.IsNullOrEmpty(LumaBoothApiPasswordBox.Password))
        {
            LumaBoothApiPasswordBox.Password = string.Empty;
        }
    }

    private Forms.NotifyIcon CreateNotifyIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open Control Center", null, (_, _) => Dispatcher.Invoke(ShowControlCenter));
        menu.Items.Add("Start Booth", null, (_, _) => Dispatcher.Invoke(() => ExecuteIfAvailable(viewModel.StartBoothCommand)));
        menu.Items.Add("Stop Booth", null, (_, _) => Dispatcher.Invoke(() => ExecuteIfAvailable(viewModel.StopBoothCommand)));
        menu.Items.Add("Relaunch Booth", null, (_, _) => Dispatcher.Invoke(() => ExecuteIfAvailable(viewModel.RestartBoothCommand)));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Dispatcher.Invoke(CloseFromTray));

        var trayIcon = new Forms.NotifyIcon
        {
            ContextMenuStrip = menu,
            Icon = System.Drawing.SystemIcons.Application,
            Text = "PhotoBIZ Agent",
            Visible = true
        };
        trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowControlCenter);
        return trayIcon;
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            HideToTray();
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (allowClose || !viewModel.IsRuntimeRunning)
        {
            return;
        }

        e.Cancel = true;
        HideToTray();
        notifyIcon.ShowBalloonTip(
            3000,
            "PhotoBIZ Agent is still running",
            "Use Stop Booth before exiting, or choose Exit from the tray menu to stop and close.",
            Forms.ToolTipIcon.Info);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        Dispose();
    }

    private void HideToTray()
    {
        Hide();
        ShowInTaskbar = false;
    }

    private void ShowControlCenter()
    {
        Show();
        ShowInTaskbar = true;
        WindowState = WindowState.Normal;
        Activate();
    }

    private async void CloseFromTray()
    {
        allowClose = true;
        await viewModel.StopRuntimeForExitAsync();
        Close();
    }

    private static void ExecuteIfAvailable(System.Windows.Input.ICommand command)
    {
        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    public void Dispose()
    {
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
        GC.SuppressFinalize(this);
    }
}
