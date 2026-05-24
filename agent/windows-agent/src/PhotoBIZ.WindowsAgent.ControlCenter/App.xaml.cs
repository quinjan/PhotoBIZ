using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PhotoBIZ.WindowsAgent;

namespace PhotoBIZ.WindowsAgent.ControlCenter;

public partial class App : System.Windows.Application
{
    private IHost? host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            host = Host.CreateDefaultBuilder(e.Args)
                .UseContentRoot(AppContext.BaseDirectory)
                .ConfigureAppConfiguration((context, configuration) =>
                {
                    configuration.SetBasePath(AppContext.BaseDirectory);
                    configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
                    configuration.AddJsonFile(
                        $"appsettings.{context.HostingEnvironment.EnvironmentName}.json",
                        optional: true,
                        reloadOnChange: false);
                    configuration.AddEnvironmentVariables();
                    configuration.AddCommandLine(e.Args);
                })
                .ConfigureServices((context, services) =>
                {
                    services.Configure<PhotoBizAgentOptions>(context.Configuration.GetSection("PhotoBIZ"));
                    services.AddHttpClient<IPhotoBizAgentApiClient, PhotoBizAgentApiClient>();
                    services.AddHttpClient<DslrBoothApiClient>();
                    services.AddHttpClient<ILumaBoothConnectionTester, LumaBoothConnectionTester>();
                    services.AddSingleton<IAgentDataPaths, AgentDataPaths>();
                    services.AddSingleton<IAgentSecretProtector, WindowsDpapiAgentSecretProtector>();
                    services.AddSingleton<IAgentConfigurationStore, FileAgentConfigurationStore>();
                    services.AddSingleton<IAgentRuntimeOptionsProvider, AgentRuntimeOptionsProvider>();
                    services.AddSingleton<IAgentDiagnosticsSanitizer, AgentDiagnosticsSanitizer>();
                    services.AddSingleton<IAgentDiagnosticsExporter, AgentDiagnosticsExporter>();
                    services.AddSingleton<SimulatorLumaBoothClient>();
                    services.AddTransient<ILumaBoothClient, ConfiguredLumaBoothClient>();
                    services.AddSingleton<IActiveLumaBoothSessionStore, FileActiveLumaBoothSessionStore>();
                    services.AddSingleton<IWindowFocusService, WindowFocusService>();
                    services.AddSingleton<IBoothUiLaunchStateStore, FileBoothUiLaunchStateStore>();
                    services.AddSingleton<IBoothUiLauncher, ChromeBoothUiLauncher>();
                    services.AddSingleton<ILumaBoothTriggerListener, LumaBoothTriggerListenerService>();
                    services.AddSingleton<LumaBoothTriggerHandler>();
                    services.AddSingleton<IAgentBoothRuntime, AgentBoothRuntime>();
                    services.AddTransient<IAgentPairingService, AgentPairingService>();
                    services.AddSingleton<AgentControlCenterViewModel>();
                    services.AddSingleton<MainWindow>();
                })
                .Build();

            await host.StartAsync();
            MainWindow = host.Services.GetRequiredService<MainWindow>();
            MainWindow.Show();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                ex.Message,
                "PhotoBIZ Agent",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (host is not null)
        {
            await host.StopAsync(TimeSpan.FromSeconds(5));
            host.Dispose();
        }

        base.OnExit(e);
    }
}
