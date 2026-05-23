using PhotoBIZ.WindowsAgent;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.Configure<PhotoBizAgentOptions>(builder.Configuration.GetSection("PhotoBIZ"));
builder.Services.AddHttpClient<IPhotoBizAgentApiClient, PhotoBizAgentApiClient>();
builder.Services.AddHttpClient<DslrBoothApiClient>();
builder.Services.AddSingleton<IAgentDataPaths, AgentDataPaths>();
builder.Services.AddSingleton<IAgentSecretProtector, WindowsDpapiAgentSecretProtector>();
builder.Services.AddSingleton<IAgentConfigurationStore, FileAgentConfigurationStore>();
builder.Services.AddSingleton<IAgentRuntimeOptionsProvider, AgentRuntimeOptionsProvider>();
builder.Services.AddSingleton<SimulatorLumaBoothClient>();
builder.Services.AddTransient<ILumaBoothClient, ConfiguredLumaBoothClient>();
builder.Services.AddSingleton<IActiveLumaBoothSessionStore, FileActiveLumaBoothSessionStore>();
builder.Services.AddSingleton<IWindowFocusService, WindowFocusService>();
builder.Services.AddSingleton<IBoothUiLauncher, ChromeBoothUiLauncher>();
builder.Services.AddSingleton<ILumaBoothTriggerListener, LumaBoothTriggerListenerService>();
builder.Services.AddSingleton<LumaBoothTriggerHandler>();
builder.Services.AddSingleton<IAgentBoothRuntime, AgentBoothRuntime>();
builder.Services.AddTransient<IAgentPairingService, AgentPairingService>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
