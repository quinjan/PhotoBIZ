using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using PhotoBIZ.WindowsAgent;

namespace PhotoBIZ.WindowsAgent.ControlCenter;

public sealed class AgentControlCenterViewModel : INotifyPropertyChanged
{
    private readonly IAgentConfigurationStore configurationStore;
    private readonly IAgentRuntimeOptionsProvider optionsProvider;
    private readonly IAgentPairingService pairingService;
    private readonly IAgentBoothRuntime runtime;
    private readonly string agentVersion = AgentMetadata.Version;

    private string apiBaseUrl = string.Empty;
    private string boothCode = string.Empty;
    private string agentCredential = string.Empty;
    private string boothUiBaseUrl = string.Empty;
    private string chromeExecutablePath = string.Empty;
    private string chromeUserDataDir = string.Empty;
    private string lumaBoothMode = string.Empty;
    private string lumaBoothApiBaseUrl = string.Empty;
    private string lumaBoothTriggerListenerUrl = string.Empty;
    private string statusMessage = "Loading...";
    private bool hasAgentCredential;
    private bool hasLumaBoothPassword;
    private bool kioskMode;
    private bool isBusy;
    private DateTimeOffset? lastUpdated;

    public AgentControlCenterViewModel(
        IAgentConfigurationStore configurationStore,
        IAgentRuntimeOptionsProvider optionsProvider,
        IAgentPairingService pairingService,
        IAgentBoothRuntime runtime)
    {
        this.configurationStore = configurationStore;
        this.optionsProvider = optionsProvider;
        this.pairingService = pairingService;
        this.runtime = runtime;

        StartBoothCommand = new AsyncRelayCommand(StartBoothAsync, () => !IsBusy && HasAgentCredential && !runtime.IsRunning);
        StopBoothCommand = new AsyncRelayCommand(StopBoothAsync, () => !IsBusy && runtime.IsRunning);
        PairCommand = new AsyncRelayCommand(PairAsync, CanSubmitPairing);
        RePairCommand = new AsyncRelayCommand(RePairAsync, CanSubmitPairing);
        ReloadCommand = new AsyncRelayCommand(ReloadAsync, () => !IsBusy);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand StartBoothCommand { get; }
    public ICommand StopBoothCommand { get; }
    public ICommand PairCommand { get; }
    public ICommand RePairCommand { get; }
    public ICommand ReloadCommand { get; }

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

    public string AgentCredential
    {
        get => agentCredential;
        set => SetField(ref agentCredential, value);
    }

    public string BoothUiBaseUrl
    {
        get => boothUiBaseUrl;
        private set => SetField(ref boothUiBaseUrl, value);
    }

    public string ChromeExecutablePath
    {
        get => string.IsNullOrWhiteSpace(chromeExecutablePath) ? "Auto-detect" : chromeExecutablePath;
        private set => SetField(ref chromeExecutablePath, value);
    }

    public string ChromeUserDataDir
    {
        get => chromeUserDataDir;
        private set => SetField(ref chromeUserDataDir, value);
    }

    public string LumaBoothMode
    {
        get => lumaBoothMode;
        private set => SetField(ref lumaBoothMode, value);
    }

    public string LumaBoothApiBaseUrl
    {
        get => lumaBoothApiBaseUrl;
        private set => SetField(ref lumaBoothApiBaseUrl, value);
    }

    public string LumaBoothTriggerListenerUrl
    {
        get => lumaBoothTriggerListenerUrl;
        private set => SetField(ref lumaBoothTriggerListenerUrl, value);
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

    public string RuntimeStatusText => runtime.IsRunning ? "Online" : "Offline";

    public string PairingStatusText => HasAgentCredential ? "Paired" : "Not paired";

    public string PairedBoothText => HasAgentCredential && !string.IsNullOrWhiteSpace(BoothCode)
        ? $"Paired to {BoothCode}"
        : "Not paired";

    public string KioskModeText => kioskMode ? "Kiosk" : "Windowed";

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
            summary.Append("Booth: ").AppendLine(BoothCode);
            summary.Append("Pairing: ").AppendLine(PairingStatusText);
            summary.Append("API: ").AppendLine(ApiBaseUrl);
            summary.Append("Booth UI: ").AppendLine(BoothUiBaseUrl);
            summary.Append("LumaBooth: ").AppendLine(LumaBoothMode);
            summary.Append("Chrome Profile: ").AppendLine(ChromeUserDataDir);
            return summary.ToString();
        }
    }

    public async Task InitializeAsync()
    {
        await ReloadAsync();
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
            ApplySnapshot(result.Configuration);
            StatusMessage = $"Re-paired to {result.BoothName} ({result.BoothCode}).";
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

    private async Task RunBusyAsync(Func<Task> action)
    {
        try
        {
            IsBusy = true;
            await action();
        }
        catch (Exception ex)
        {
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
        kioskMode = useRuntimeOptions ? runtimeOptions!.Display.KioskMode : snapshot.Display.KioskMode;
        lastUpdated = DateTimeOffset.Now;

        OnPropertyChanged(nameof(LumaBoothPasswordStatusText));
        OnPropertyChanged(nameof(KioskModeText));
        OnPropertyChanged(nameof(LastUpdatedText));
        OnPropertyChanged(nameof(DiagnosticSummaryText));
        RefreshRuntimeState();
    }

    private void RefreshRuntimeState()
    {
        OnPropertyChanged(nameof(RuntimeStatusText));
        OnPropertyChanged(nameof(PairedBoothText));
        OnPropertyChanged(nameof(RecentLogText));
        OnPropertyChanged(nameof(DiagnosticSummaryText));
        RefreshCommands();
    }

    private void RefreshCommands()
    {
        (StartBoothCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (StopBoothCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (PairCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (RePairCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
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

        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
