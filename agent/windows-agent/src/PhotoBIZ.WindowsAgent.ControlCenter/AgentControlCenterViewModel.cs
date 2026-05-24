using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using PhotoBIZ.WindowsAgent;

namespace PhotoBIZ.WindowsAgent.ControlCenter;

public sealed class AgentControlCenterViewModel : INotifyPropertyChanged
{
    private readonly IAgentConfigurationStore configurationStore;
    private readonly IAgentRuntimeOptionsProvider optionsProvider;
    private readonly IAgentPairingService pairingService;
    private readonly IAgentBoothRuntime runtime;
    private readonly ILumaBoothConnectionTester lumaBoothConnectionTester;
    private readonly IAgentDiagnosticsExporter diagnosticsExporter;
    private readonly string agentVersion = AgentMetadata.Version;

    private string apiBaseUrl = string.Empty;
    private string boothCode = string.Empty;
    private string boothName = string.Empty;
    private string agentCredential = string.Empty;
    private string boothUiBaseUrl = string.Empty;
    private string chromeExecutablePath = string.Empty;
    private string chromeUserDataDir = string.Empty;
    private string lumaBoothMode = string.Empty;
    private string lumaBoothApiBaseUrl = string.Empty;
    private string lumaBoothApiPassword = string.Empty;
    private string lumaBoothTriggerListenerUrl = string.Empty;
    private string statusMessage = "Loading...";
    private bool hasAgentCredential;
    private bool hasLumaBoothPassword;
    private bool kioskMode;
    private bool launchBoothUiOnStartup;
    private bool isBusy;
    private bool rePairRequired;
    private bool technicianMode;
    private int pollIntervalSeconds = 5;
    private int simulatedSessionDurationSeconds = 6;
    private int lumaBoothStartTimeoutSeconds = 15;
    private DateTimeOffset? lastUpdated;

    public AgentControlCenterViewModel(
        IAgentConfigurationStore configurationStore,
        IAgentRuntimeOptionsProvider optionsProvider,
        IAgentPairingService pairingService,
        IAgentBoothRuntime runtime,
        ILumaBoothConnectionTester lumaBoothConnectionTester,
        IAgentDiagnosticsExporter diagnosticsExporter)
    {
        this.configurationStore = configurationStore;
        this.optionsProvider = optionsProvider;
        this.pairingService = pairingService;
        this.runtime = runtime;
        this.lumaBoothConnectionTester = lumaBoothConnectionTester;
        this.diagnosticsExporter = diagnosticsExporter;

        StartBoothCommand = new AsyncRelayCommand(StartBoothAsync, () => !IsBusy && HasAgentCredential && !RePairRequired && !runtime.IsRunning);
        StopBoothCommand = new AsyncRelayCommand(StopBoothAsync, () => !IsBusy && runtime.IsRunning);
        RestartBoothCommand = new AsyncRelayCommand(RestartBoothAsync, () => !IsBusy && runtime.IsRunning);
        PairCommand = new AsyncRelayCommand(PairAsync, CanSubmitPairing);
        RePairCommand = new AsyncRelayCommand(RePairAsync, CanSubmitPairing);
        DetectChromeCommand = new AsyncRelayCommand(DetectChromeAsync, CanSaveSettings);
        SaveDisplaySettingsCommand = new AsyncRelayCommand(SaveDisplaySettingsAsync, CanSaveSettings);
        SaveLumaBoothSettingsCommand = new AsyncRelayCommand(SaveLumaBoothSettingsAsync, CanSaveSettings);
        TestLumaBoothCommand = new AsyncRelayCommand(TestLumaBoothAsync, () => !IsBusy);
        ExportDiagnosticsCommand = new AsyncRelayCommand(ExportDiagnosticsAsync, () => !IsBusy);
        ReloadCommand = new AsyncRelayCommand(ReloadAsync, () => !IsBusy);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand StartBoothCommand { get; }
    public ICommand StopBoothCommand { get; }
    public ICommand RestartBoothCommand { get; }
    public ICommand PairCommand { get; }
    public ICommand RePairCommand { get; }
    public ICommand DetectChromeCommand { get; }
    public ICommand SaveDisplaySettingsCommand { get; }
    public ICommand SaveLumaBoothSettingsCommand { get; }
    public ICommand TestLumaBoothCommand { get; }
    public ICommand ExportDiagnosticsCommand { get; }
    public ICommand ReloadCommand { get; }

    public IReadOnlyList<string> LumaBoothModeOptions { get; } =
    [
        LumaBoothIntegrationMode.Simulator,
        LumaBoothIntegrationMode.Api
    ];

    public string AgentVersion => agentVersion;

    public string ApiBaseUrl
    {
        get => apiBaseUrl;
        set => SetField(ref apiBaseUrl, value);
    }

    public string BoothCode
    {
        get => boothCode;
        set => SetField(ref boothCode, value);
    }

    public string BoothName
    {
        get => boothName;
        private set
        {
            if (SetField(ref boothName, value))
            {
                OnPropertyChanged(nameof(PairedBoothText));
            }
        }
    }

    public string AgentCredential
    {
        get => agentCredential;
        set => SetField(ref agentCredential, value);
    }

    public string BoothUiBaseUrl
    {
        get => boothUiBaseUrl;
        set => SetField(ref boothUiBaseUrl, value);
    }

    public string ChromeExecutablePath
    {
        get => chromeExecutablePath;
        set => SetField(ref chromeExecutablePath, value);
    }

    public string ChromeExecutablePathText => string.IsNullOrWhiteSpace(ChromeExecutablePath) ? "Auto-detect" : ChromeExecutablePath;

    public string ChromeUserDataDirText => string.IsNullOrWhiteSpace(ChromeUserDataDir) ? "Default ProgramData profile" : ChromeUserDataDir;

    public bool LaunchBoothUiOnStartup
    {
        get => launchBoothUiOnStartup;
        set
        {
            if (SetField(ref launchBoothUiOnStartup, value))
            {
                OnPropertyChanged(nameof(LaunchBoothUiOnStartupText));
            }
        }
    }

    public string LaunchBoothUiOnStartupText => LaunchBoothUiOnStartup ? "Enabled" : "Disabled";

    public bool KioskMode
    {
        get => kioskMode;
        set
        {
            if (SetField(ref kioskMode, value))
            {
                OnPropertyChanged(nameof(KioskModeText));
            }
        }
    }

    public string ChromeUserDataDir
    {
        get => chromeUserDataDir;
        set => SetField(ref chromeUserDataDir, value);
    }

    public string LumaBoothMode
    {
        get => lumaBoothMode;
        set => SetField(ref lumaBoothMode, value);
    }

    public string LumaBoothApiBaseUrl
    {
        get => lumaBoothApiBaseUrl;
        set => SetField(ref lumaBoothApiBaseUrl, value);
    }

    public string LumaBoothApiPassword
    {
        get => lumaBoothApiPassword;
        set => SetField(ref lumaBoothApiPassword, value);
    }

    public string LumaBoothTriggerListenerUrl
    {
        get => lumaBoothTriggerListenerUrl;
        set => SetField(ref lumaBoothTriggerListenerUrl, value);
    }

    public int LumaBoothStartTimeoutSeconds
    {
        get => lumaBoothStartTimeoutSeconds;
        set => SetField(ref lumaBoothStartTimeoutSeconds, Math.Max(1, value));
    }

    public string StatusMessage
    {
        get => statusMessage;
        private set => SetField(ref statusMessage, value);
    }

    public bool HasAgentCredential
    {
        get => hasAgentCredential;
        private set
        {
            if (SetField(ref hasAgentCredential, value))
            {
                OnPropertyChanged(nameof(PairingStatusText));
                OnPropertyChanged(nameof(PairedBoothText));
            }
        }
    }

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (SetField(ref isBusy, value))
            {
                RefreshCommands();
            }
        }
    }

    public bool RePairRequired
    {
        get => rePairRequired;
        private set
        {
            if (SetField(ref rePairRequired, value))
            {
                OnPropertyChanged(nameof(PairingStatusText));
                OnPropertyChanged(nameof(PairedBoothText));
                RefreshCommands();
            }
        }
    }

    public bool TechnicianMode
    {
        get => technicianMode;
        set
        {
            if (SetField(ref technicianMode, value))
            {
                OnPropertyChanged(nameof(TechnicianVisibility));
                OnPropertyChanged(nameof(ModeText));
            }
        }
    }

    public Visibility TechnicianVisibility => TechnicianMode ? Visibility.Visible : Visibility.Collapsed;

    public string ModeText => TechnicianMode ? "Technician" : "Staff";

    public bool IsRuntimeRunning => runtime.IsRunning;

    public string RuntimeStatusText => runtime.IsRunning ? "Online" : "Offline";

    public string PairingStatusText => RePairRequired ? "Re-pair required" : HasAgentCredential ? "Paired" : "Not paired";

    public string PairedBoothText => RePairRequired
        ? $"Re-pair required for {BoothDisplayText}"
        : HasAgentCredential && !string.IsNullOrWhiteSpace(BoothCode)
        ? $"Paired to {BoothDisplayText}"
        : "Not paired";

    private string BoothDisplayText => string.IsNullOrWhiteSpace(BoothName)
        ? BoothCode
        : $"{BoothName} ({BoothCode})";

    public string KioskModeText => KioskMode ? "Kiosk" : "Windowed";

    public string LumaBoothPasswordStatusText => hasLumaBoothPassword ? "Saved" : "Not saved";

    public string LastUpdatedText => lastUpdated?.ToLocalTime().ToString("g", CultureInfo.CurrentCulture) ?? "Not loaded";

    public string RecentLogText => StatusMessage;

    public string DiagnosticSummaryText
    {
        get
        {
            var summary = new StringBuilder();
            summary.Append("Version: ").AppendLine(AgentVersion);
            summary.Append("Runtime: ").AppendLine(RuntimeStatusText);
            summary.Append("Booth: ").AppendLine(BoothDisplayText);
            summary.Append("Pairing: ").AppendLine(PairingStatusText);
            summary.Append("API: ").AppendLine(ApiBaseUrl);
            summary.Append("Booth UI: ").AppendLine(BoothUiBaseUrl);
            summary.Append("LumaBooth: ").AppendLine(LumaBoothMode);
            summary.Append("Chrome Profile: ").AppendLine(ChromeUserDataDirText);
            summary.Append("Launch Booth UI: ").AppendLine(LaunchBoothUiOnStartupText);
            return summary.ToString();
        }
    }

    public async Task InitializeAsync()
    {
        await ReloadAsync();
    }

    public async Task StopRuntimeForExitAsync()
    {
        if (!runtime.IsRunning)
        {
            return;
        }

        await StopBoothAsync();
    }

    private async Task ReloadAsync()
    {
        await RunBusyAsync(async () =>
        {
            var snapshot = await configurationStore.LoadSnapshotAsync(CancellationToken.None);
            var runtimeOptions = await optionsProvider.LoadAsync(CancellationToken.None);
            ApplyConfiguration(snapshot, runtimeOptions);
            StatusMessage = HasAgentCredential
                ? "Ready."
                : "Pair with an Agent credential before starting the booth.";
        });
    }

    private async Task PairAsync()
    {
        await RunBusyAsync(async () =>
        {
            var result = await pairingService.PairAsync(BuildPairingRequest(), CancellationToken.None);
            AgentCredential = string.Empty;
            RePairRequired = false;
            ApplySnapshot(result.Configuration);
            StatusMessage = $"Paired to {result.BoothName} ({result.BoothCode}).";
        });
    }

    private async Task RePairAsync()
    {
        await RunBusyAsync(async () =>
        {
            var result = await pairingService.RePairAsync(BuildPairingRequest(), CancellationToken.None);
            AgentCredential = string.Empty;
            RePairRequired = false;
            ApplySnapshot(result.Configuration);
            StatusMessage = $"Re-paired to {result.BoothName} ({result.BoothCode}).";
        });
    }

    private async Task SaveDisplaySettingsAsync()
    {
        await SaveSettingsAsync("Kiosk/display settings saved.");
    }

    private Task DetectChromeAsync()
    {
        var resolved = ChromeBoothUiLauncher.ResolveChromePath(ChromeExecutablePath);
        ChromeExecutablePath = string.Equals(resolved, "chrome.exe", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : resolved;
        StatusMessage = string.IsNullOrWhiteSpace(ChromeExecutablePath)
            ? "Chrome will be resolved from PATH at launch."
            : $"Chrome detected at {ChromeExecutablePath}.";
        return Task.CompletedTask;
    }

    private async Task SaveLumaBoothSettingsAsync()
    {
        await SaveSettingsAsync("LumaBooth settings saved.");
        LumaBoothApiPassword = string.Empty;
    }

    private async Task TestLumaBoothAsync()
    {
        await RunBusyAsync(async () =>
        {
            var result = await lumaBoothConnectionTester.TestAsync(CancellationToken.None);
            StatusMessage = result.Message;
        });
    }

    private async Task ExportDiagnosticsAsync()
    {
        await RunBusyAsync(async () =>
        {
            var result = await diagnosticsExporter.ExportAsync(CancellationToken.None);
            StatusMessage = $"Diagnostics exported to {result.FilePath}.";
        });
    }

    private async Task SaveSettingsAsync(string successMessage)
    {
        await RunBusyAsync(async () =>
        {
            await configurationStore.SaveAsync(BuildConfigurationUpdate(), CancellationToken.None);
            var snapshot = await configurationStore.LoadSnapshotAsync(CancellationToken.None);
            var runtimeOptions = await optionsProvider.LoadAsync(CancellationToken.None);
            ApplyConfiguration(snapshot, runtimeOptions);
            StatusMessage = successMessage;
        });
    }

    private async Task StartBoothAsync()
    {
        await RunBusyAsync(async () =>
        {
            await runtime.StartAsync(CancellationToken.None);
            StatusMessage = "Booth runtime started.";
            RefreshRuntimeState();
        });
    }

    private async Task StopBoothAsync()
    {
        await RunBusyAsync(async () =>
        {
            await runtime.StopAsync(CancellationToken.None);
            StatusMessage = "Booth runtime stopped.";
            RefreshRuntimeState();
        });
    }

    private async Task RestartBoothAsync()
    {
        await RunBusyAsync(async () =>
        {
            await runtime.StopAsync(CancellationToken.None);
            await runtime.StartAsync(CancellationToken.None);
            StatusMessage = "Booth runtime relaunched.";
            RefreshRuntimeState();
        });
    }

    private AgentPairingRequest BuildPairingRequest()
    {
        return new AgentPairingRequest(ApiBaseUrl, BoothCode, AgentCredential);
    }

    private bool CanSubmitPairing()
    {
        return !IsBusy &&
            !string.IsNullOrWhiteSpace(ApiBaseUrl) &&
            !string.IsNullOrWhiteSpace(BoothCode) &&
            !string.IsNullOrWhiteSpace(AgentCredential);
    }

    private bool CanSaveSettings()
    {
        return !IsBusy && !runtime.IsRunning;
    }

    private AgentConfigurationUpdate BuildConfigurationUpdate()
    {
        return new AgentConfigurationUpdate(
            ApiBaseUrl,
            BoothCode,
            BoothName,
            AgentCredential: null,
            pollIntervalSeconds,
            simulatedSessionDurationSeconds,
            new LumaBoothConfigurationUpdate(
                LumaBoothMode,
                LumaBoothApiBaseUrl,
                string.IsNullOrEmpty(LumaBoothApiPassword) ? null : LumaBoothApiPassword,
                LumaBoothTriggerListenerUrl,
                LumaBoothStartTimeoutSeconds),
            new DisplayConfigurationUpdate(
                "dslrBooth",
                "BoothUi",
                BoothUiBaseUrl,
                ChromeExecutablePath,
                ChromeUserDataDir,
                LaunchBoothUiOnStartup,
                KioskMode));
    }

    private async Task RunBusyAsync(Func<Task> action)
    {
        try
        {
            IsBusy = true;
            await action();
        }
        catch (Exception ex)
        {
            if (ex is AgentCredentialUnauthorizedException)
            {
                RePairRequired = true;
            }

            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            RefreshRuntimeState();
        }
    }

    private void ApplySnapshot(AgentConfigurationSnapshot snapshot)
    {
        ApplyConfiguration(snapshot, null);
    }

    private void ApplyConfiguration(AgentConfigurationSnapshot snapshot, PhotoBizAgentOptions? runtimeOptions)
    {
        var useRuntimeOptions = runtimeOptions is not null && !snapshot.HasAgentCredential;

        ApiBaseUrl = useRuntimeOptions ? runtimeOptions!.ApiBaseUrl : snapshot.ApiBaseUrl;
        BoothCode = useRuntimeOptions ? runtimeOptions!.BoothCode : snapshot.BoothCode;
        BoothName = useRuntimeOptions ? runtimeOptions!.BoothName : snapshot.BoothName;
        HasAgentCredential = snapshot.HasAgentCredential ||
            !string.IsNullOrWhiteSpace(runtimeOptions?.AgentCredential);
        BoothUiBaseUrl = useRuntimeOptions ? runtimeOptions!.Display.BoothUiBaseUrl : snapshot.Display.BoothUiBaseUrl;
        ChromeExecutablePath = useRuntimeOptions ? runtimeOptions!.Display.ChromeExecutablePath : snapshot.Display.ChromeExecutablePath;
        ChromeUserDataDir = useRuntimeOptions ? runtimeOptions!.Display.ChromeUserDataDir : snapshot.Display.ChromeUserDataDir;
        LumaBoothMode = useRuntimeOptions ? runtimeOptions!.LumaBooth.Mode : snapshot.LumaBooth.Mode;
        LumaBoothApiBaseUrl = useRuntimeOptions ? runtimeOptions!.LumaBooth.ApiBaseUrl : snapshot.LumaBooth.ApiBaseUrl;
        LumaBoothTriggerListenerUrl = useRuntimeOptions
            ? runtimeOptions!.LumaBooth.TriggerListenerUrl
            : snapshot.LumaBooth.TriggerListenerUrl;
        hasLumaBoothPassword = snapshot.LumaBooth.HasApiPassword;
        KioskMode = useRuntimeOptions ? runtimeOptions!.Display.KioskMode : snapshot.Display.KioskMode;
        LaunchBoothUiOnStartup = useRuntimeOptions
            ? runtimeOptions!.Display.LaunchBoothUiOnStartup
            : snapshot.Display.LaunchBoothUiOnStartup;
        pollIntervalSeconds = useRuntimeOptions ? runtimeOptions!.PollIntervalSeconds : snapshot.PollIntervalSeconds;
        simulatedSessionDurationSeconds = useRuntimeOptions
            ? runtimeOptions!.SimulatedSessionDurationSeconds
            : snapshot.SimulatedSessionDurationSeconds;
        LumaBoothStartTimeoutSeconds = useRuntimeOptions
            ? runtimeOptions!.LumaBooth.StartTimeoutSeconds
            : snapshot.LumaBooth.StartTimeoutSeconds;
        lastUpdated = DateTimeOffset.Now;

        OnPropertyChanged(nameof(LumaBoothPasswordStatusText));
        OnPropertyChanged(nameof(KioskModeText));
        OnPropertyChanged(nameof(ChromeExecutablePathText));
        OnPropertyChanged(nameof(ChromeUserDataDirText));
        OnPropertyChanged(nameof(LaunchBoothUiOnStartupText));
        OnPropertyChanged(nameof(LastUpdatedText));
        OnPropertyChanged(nameof(DiagnosticSummaryText));
        RefreshRuntimeState();
    }

    private void RefreshRuntimeState()
    {
        OnPropertyChanged(nameof(RuntimeStatusText));
        OnPropertyChanged(nameof(IsRuntimeRunning));
        OnPropertyChanged(nameof(PairedBoothText));
        OnPropertyChanged(nameof(RecentLogText));
        OnPropertyChanged(nameof(DiagnosticSummaryText));
        RefreshCommands();
    }

    private void RefreshCommands()
    {
        (StartBoothCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (StopBoothCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (RestartBoothCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (PairCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (RePairCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (DetectChromeCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (SaveDisplaySettingsCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (SaveLumaBoothSettingsCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (TestLumaBoothCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (ExportDiagnosticsCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (ReloadCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);

        if (propertyName is nameof(ApiBaseUrl) or nameof(BoothCode) or nameof(AgentCredential))
        {
            RefreshCommands();
        }

        if (propertyName is nameof(ChromeExecutablePath))
        {
            OnPropertyChanged(nameof(ChromeExecutablePathText));
        }

        if (propertyName is nameof(ChromeUserDataDir))
        {
            OnPropertyChanged(nameof(ChromeUserDataDirText));
        }

        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
