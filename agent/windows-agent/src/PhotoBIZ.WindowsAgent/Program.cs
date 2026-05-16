using PhotoBIZ.WindowsAgent;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.Configure<PhotoBizAgentOptions>(builder.Configuration.GetSection("PhotoBIZ"));
builder.Services.AddHttpClient("photobiz-agent");
builder.Services.AddHostedService<Worker>();

if (OperatingSystem.IsWindows())
{
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "PhotoBIZ Windows Booth Agent";
    });
}

var host = builder.Build();
host.Run();
