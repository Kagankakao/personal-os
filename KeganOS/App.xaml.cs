using KeganOS.Core.Interfaces;
using KeganOS.Infrastructure.Data;
using KeganOS.Infrastructure.Services;
using KeganOS.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace KeganOS;

/// <summary>
/// Application entry point with DI and Serilog
/// </summary>
public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File("logs/keganos-.log", 
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .Enrich.FromLogContext()
            .CreateLogger();

        Log.Information("KeganOS starting up...");

        // Configure Host with DI
        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices((context, services) =>
            {
                ConfigureServices(services);
            })
            .Build();

        _host.Start();

        // Initialize database
        var db = _host.Services.GetRequiredService<AppDbContext>();
        db.Initialize();
        Log.Information("Database initialized");

        // Show profile selection first
        var userService = _host.Services.GetRequiredService<IUserService>();
        var profileWindow = new ProfileSelectionWindow(userService);
        
        if (profileWindow.ShowDialog() == true && profileWindow.SelectedUser != null)
        {
            // User selected, open main window
            var mainWindow = new MainWindow();
            mainWindow.SetCurrentUser(profileWindow.SelectedUser);
            mainWindow.Show();
            Log.Information("Main window displayed for user: {User}", profileWindow.SelectedUser.DisplayName);
        }
        else
        {
            // No user selected, shutdown
            Log.Information("No user selected, shutting down");
            Shutdown();
        }
    }

    private void ConfigureServices(IServiceCollection services)
    {
        Log.Debug("Configuring services...");

        // Database
        services.AddSingleton<AppDbContext>();
        
        // Services
        services.AddSingleton<IUserService, UserService>();
        services.AddSingleton<IJournalService, JournalService>();
        services.AddSingleton<IPixelaService, PixelaService>();
        services.AddSingleton<IKegomoDoroService, KegomoDoroService>();
        services.AddSingleton<IAIProvider>(sp => new Infrastructure.AI.GeminiProvider());

        Log.Debug("Services configured successfully");
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        Log.Information("Application shutting down...");
        
        _host?.StopAsync(TimeSpan.FromSeconds(5)).Wait();
        _host?.Dispose();
        
        Log.CloseAndFlush();
        
        base.OnExit(e);
    }
}
