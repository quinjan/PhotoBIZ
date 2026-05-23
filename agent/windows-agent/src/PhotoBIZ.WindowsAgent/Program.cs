using PhotoBIZ.WindowsAgent;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.Configure<PhotoBizAgentOptions>(builder.Configuration.GetSection("PhotoBIZ"));
builder.Services.AddHttpClient<IPhotoBizAgentApiClient, PhotoBizAgentApiClient>();
builder.Services.AddHttpClient<DslrBoothApiClient>();
builder.Services.AddSingleton<SimulatorLumaBoothClient>();
builder.Services.AddTransient<ILumaBoothClient>(services =>
{
    var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<PhotoBizAgentOptions>>().Value;
    return string.Equals(options.LumaBooth.Mode, LumaBoothIntegrationMode.Api, StringComparison.OrdinalIgnoreCase)
        ? services.GetRequiredService<DslrBoothApiClient>()
        : services.GetRequiredService<SimulatorLumaBoothClient>();
});
builder.Services.AddSingleton<IActiveLumaBoothSessionStore, FileActiveLumaBoothSessionStore>();
builder.Services.AddSingleton<IWindowFocusService, WindowFocusService>();
builder.Services.AddSingleton<IBoothUiLauncher, ChromeBoothUiLauncher>();
builder.Services.AddSingleton<ILumaBoothTriggerListener, LumaBoothTriggerListenerService>();
builder.Services.AddSingleton<LumaBoothTriggerHandler>();
builder.Services.AddSingleton<IAgentBoothRuntime, AgentBoothRuntime>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
