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
builder.Services.AddSingleton<LumaBoothTriggerHandler>();
builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<LumaBoothTriggerListenerService>();

if (OperatingSystem.IsWindows())
{
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "PhotoBIZ Windows Booth Agent";
    });
}

var host = builder.Build();
host.Run();
