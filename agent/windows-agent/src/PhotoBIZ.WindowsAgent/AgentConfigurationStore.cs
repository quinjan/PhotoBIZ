using System.Text.Json;

namespace PhotoBIZ.WindowsAgent;

public interface IAgentConfigurationStore
{
    Task<AgentConfigurationSnapshot> LoadSnapshotAsync(CancellationToken cancellationToken);
    Task<PhotoBizAgentOptions> LoadRuntimeOptionsAsync(CancellationToken cancellationToken);
    Task SaveAsync(AgentConfigurationUpdate update, CancellationToken cancellationToken);
    Task ClearAsync(CancellationToken cancellationToken);
}

public sealed record AgentConfigurationSnapshot(
    string ApiBaseUrl,
    string BoothCode,
    string BoothName,
    bool HasAgentCredential,
    int PollIntervalSeconds,
    int SimulatedSessionDurationSeconds,
    AgentStorageConfigurationSnapshot Storage,
    LumaBoothConfigurationSnapshot LumaBooth,
    DisplayConfigurationSnapshot Display);

public sealed record AgentStorageConfigurationSnapshot(string BaseDirectory);

public sealed record LumaBoothConfigurationSnapshot(
    string Mode,
    string ApiBaseUrl,
    bool HasApiPassword,
    string TriggerListenerUrl,
    int StartTimeoutSeconds);

public sealed record DisplayConfigurationSnapshot(
    string LumaBoothWindowTitle,
    string BoothUiWindowTitle,
    string BoothUiBaseUrl,
    string ChromeExecutablePath,
    string ChromeUserDataDir,
    bool LaunchBoothUiOnStartup,
    bool KioskMode);

public sealed record AgentConfigurationUpdate(
    string ApiBaseUrl,
    string BoothCode,
    string BoothName,
    string? AgentCredential,
    int PollIntervalSeconds,
    int SimulatedSessionDurationSeconds,
    LumaBoothConfigurationUpdate LumaBooth,
    DisplayConfigurationUpdate Display);

public sealed record LumaBoothConfigurationUpdate(
    string Mode,
    string ApiBaseUrl,
    string? ApiPassword,
    string TriggerListenerUrl,
    int StartTimeoutSeconds);

public sealed record DisplayConfigurationUpdate(
    string LumaBoothWindowTitle,
    string BoothUiWindowTitle,
    string BoothUiBaseUrl,
    string ChromeExecutablePath,
    string ChromeUserDataDir,
    bool LaunchBoothUiOnStartup,
    bool KioskMode);

public sealed class FileAgentConfigurationStore(
    IAgentDataPaths dataPaths,
    IAgentSecretProtector secretProtector) : IAgentConfigurationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<AgentConfigurationSnapshot> LoadSnapshotAsync(CancellationToken cancellationToken)
    {
        var config = await LoadFileAsync(cancellationToken);
        return ToSnapshot(config);
    }

    public async Task<PhotoBizAgentOptions> LoadRuntimeOptionsAsync(CancellationToken cancellationToken)
    {
        var config = await LoadFileAsync(cancellationToken);
        return ToRuntimeOptions(config);
    }

    public async Task SaveAsync(AgentConfigurationUpdate update, CancellationToken cancellationToken)
    {
        var existing = await LoadFileAsync(cancellationToken);
        var config = new AgentConfigurationFile
        {
            ApiBaseUrl = NormalizeRequired(update.ApiBaseUrl, "API base URL"),
            BoothCode = NormalizeRequired(update.BoothCode, "booth code").ToUpperInvariant(),
            BoothName = update.BoothName.Trim(),
            AgentCredential = ProtectOrPreserve(update.AgentCredential, existing.AgentCredential),
            PollIntervalSeconds = Math.Max(1, update.PollIntervalSeconds),
            SimulatedSessionDurationSeconds = Math.Max(1, update.SimulatedSessionDurationSeconds),
            LumaBooth = new AgentLumaBoothConfigurationFile
            {
                Mode = NormalizeLumaBoothMode(update.LumaBooth.Mode),
                ApiBaseUrl = NormalizeRequired(update.LumaBooth.ApiBaseUrl, "LumaBooth API base URL"),
                ApiPassword = ProtectOrPreserve(update.LumaBooth.ApiPassword, existing.LumaBooth.ApiPassword),
                TriggerListenerUrl = NormalizeRequired(update.LumaBooth.TriggerListenerUrl, "trigger listener URL"),
                StartTimeoutSeconds = Math.Max(1, update.LumaBooth.StartTimeoutSeconds)
            },
            Display = new AgentDisplayConfigurationFile
            {
                LumaBoothWindowTitle = update.Display.LumaBoothWindowTitle.Trim(),
                BoothUiWindowTitle = update.Display.BoothUiWindowTitle.Trim(),
                BoothUiBaseUrl = NormalizeRequired(update.Display.BoothUiBaseUrl, "Booth UI base URL"),
                ChromeExecutablePath = update.Display.ChromeExecutablePath.Trim(),
                ChromeUserDataDir = update.Display.ChromeUserDataDir.Trim(),
                LaunchBoothUiOnStartup = update.Display.LaunchBoothUiOnStartup,
                KioskMode = update.Display.KioskMode
            }
        };

        Directory.CreateDirectory(dataPaths.RootDirectory);
        await using var stream = File.Create(dataPaths.ConfigurationFilePath);
        await JsonSerializer.SerializeAsync(stream, config, JsonOptions, cancellationToken);
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(dataPaths.ConfigurationFilePath))
        {
            File.Delete(dataPaths.ConfigurationFilePath);
        }

        return Task.CompletedTask;
    }

    private async Task<AgentConfigurationFile> LoadFileAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(dataPaths.ConfigurationFilePath))
        {
            return AgentConfigurationFile.Default(dataPaths.RootDirectory);
        }

        await using var stream = File.OpenRead(dataPaths.ConfigurationFilePath);
        return await JsonSerializer.DeserializeAsync<AgentConfigurationFile>(stream, JsonOptions, cancellationToken)
            ?? AgentConfigurationFile.Default(dataPaths.RootDirectory);
    }

    private PhotoBizAgentOptions ToRuntimeOptions(AgentConfigurationFile config)
    {
        return new PhotoBizAgentOptions
        {
            ApiBaseUrl = config.ApiBaseUrl,
            BoothCode = config.BoothCode,
            BoothName = config.BoothName,
            AgentCredential = Unprotect(config.AgentCredential),
            PollIntervalSeconds = config.PollIntervalSeconds,
            SimulatedSessionDurationSeconds = config.SimulatedSessionDurationSeconds,
            Storage = new AgentStorageOptions
            {
                BaseDirectory = dataPaths.RootDirectory
            },
            LumaBooth = new LumaBoothOptions
            {
                Mode = config.LumaBooth.Mode,
                ApiBaseUrl = config.LumaBooth.ApiBaseUrl,
                ApiPassword = Unprotect(config.LumaBooth.ApiPassword),
                TriggerListenerUrl = config.LumaBooth.TriggerListenerUrl,
                StartTimeoutSeconds = config.LumaBooth.StartTimeoutSeconds
            },
            Display = new DisplayOptions
            {
                LumaBoothWindowTitle = config.Display.LumaBoothWindowTitle,
                BoothUiWindowTitle = config.Display.BoothUiWindowTitle,
                BoothUiBaseUrl = config.Display.BoothUiBaseUrl,
                ChromeExecutablePath = config.Display.ChromeExecutablePath,
                ChromeUserDataDir = config.Display.ChromeUserDataDir,
                LaunchBoothUiOnStartup = config.Display.LaunchBoothUiOnStartup,
                KioskMode = config.Display.KioskMode
            }
        };
    }

    private AgentConfigurationSnapshot ToSnapshot(AgentConfigurationFile config)
    {
        return new AgentConfigurationSnapshot(
            config.ApiBaseUrl,
            config.BoothCode,
            config.BoothName,
            !string.IsNullOrWhiteSpace(config.AgentCredential),
            config.PollIntervalSeconds,
            config.SimulatedSessionDurationSeconds,
            new AgentStorageConfigurationSnapshot(dataPaths.RootDirectory),
            new LumaBoothConfigurationSnapshot(
                config.LumaBooth.Mode,
                config.LumaBooth.ApiBaseUrl,
                !string.IsNullOrWhiteSpace(config.LumaBooth.ApiPassword),
                config.LumaBooth.TriggerListenerUrl,
                config.LumaBooth.StartTimeoutSeconds),
            new DisplayConfigurationSnapshot(
                config.Display.LumaBoothWindowTitle,
                config.Display.BoothUiWindowTitle,
                config.Display.BoothUiBaseUrl,
                config.Display.ChromeExecutablePath,
                config.Display.ChromeUserDataDir,
                config.Display.LaunchBoothUiOnStartup,
                config.Display.KioskMode));
    }

    private string ProtectOrPreserve(string? secret, string existingProtectedSecret)
    {
        if (secret is null)
        {
            return existingProtectedSecret;
        }

        return string.IsNullOrEmpty(secret) ? string.Empty : secretProtector.Protect(secret);
    }

    private string Unprotect(string protectedSecret)
    {
        return string.IsNullOrWhiteSpace(protectedSecret)
            ? string.Empty
            : secretProtector.Unprotect(protectedSecret);
    }

    private static string NormalizeRequired(string value, string fieldName)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException($"{fieldName} is required.");
        }

        return normalized;
    }

    private static string NormalizeLumaBoothMode(string mode)
    {
        return string.Equals(mode, LumaBoothIntegrationMode.Api, StringComparison.OrdinalIgnoreCase)
            ? LumaBoothIntegrationMode.Api
            : LumaBoothIntegrationMode.Simulator;
    }

    private sealed class AgentConfigurationFile
    {
        public string ApiBaseUrl { get; set; } = "https://api.photobiz.local";
        public string BoothCode { get; set; } = string.Empty;
        public string BoothName { get; set; } = string.Empty;
        public string AgentCredential { get; set; } = string.Empty;
        public int PollIntervalSeconds { get; set; } = 5;
        public int SimulatedSessionDurationSeconds { get; set; } = 6;
        public AgentLumaBoothConfigurationFile LumaBooth { get; set; } = new();
        public AgentDisplayConfigurationFile Display { get; set; } = new();

        public static AgentConfigurationFile Default(string rootDirectory)
        {
            return new AgentConfigurationFile
            {
                Display = new AgentDisplayConfigurationFile
                {
                    ChromeUserDataDir = Path.Combine(rootDirectory, "chrome-kiosk")
                }
            };
        }
    }

    private sealed class AgentLumaBoothConfigurationFile
    {
        public string Mode { get; set; } = LumaBoothIntegrationMode.Simulator;
        public string ApiBaseUrl { get; set; } = "http://localhost:1500";
        public string ApiPassword { get; set; } = string.Empty;
        public string TriggerListenerUrl { get; set; } = "http://127.0.0.1:5617/lumabooth/events";
        public int StartTimeoutSeconds { get; set; } = 15;
    }

    private sealed class AgentDisplayConfigurationFile
    {
        public string LumaBoothWindowTitle { get; set; } = "dslrBooth";
        public string BoothUiWindowTitle { get; set; } = "BoothUi";
        public string BoothUiBaseUrl { get; set; } = "https://booth.photobiz.local";
        public string ChromeExecutablePath { get; set; } = string.Empty;
        public string ChromeUserDataDir { get; set; } = string.Empty;
        public bool LaunchBoothUiOnStartup { get; set; } = true;
        public bool KioskMode { get; set; } = true;
    }
}
