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
        
        // IMPORTANT: Prevent app from auto-closing when dialog closes
        ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
        
        // Register global exception handlers FIRST
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        
        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File("logs/keganos-.log", 
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .Enrich.FromLogContext()
            .CreateLogger();

        Log.Information("KeganOS starting up...");

        try
        {
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
            var pixelaService = _host.Services.GetRequiredService<IPixelaService>();
            var profileWindow = new ProfileSelectionWindow(userService, pixelaService);
            
            if (profileWindow.ShowDialog() == true && profileWindow.SelectedUser != null)
            {
                // User selected, open main window with services
                var kegomoDoroService = _host.Services.GetRequiredService<IKegomoDoroService>();
                var journalService = _host.Services.GetRequiredService<IJournalService>();
                var aiProvider = _host.Services.GetRequiredService<IAIProvider>();
                var motivationalService = _host.Services.GetRequiredService<IMotivationalMessageService>();
                // pixelaService already declared above
                
                var mainWindow = new MainWindow(kegomoDoroService, journalService, pixelaService, aiProvider, motivationalService, userService);
                mainWindow.SetCurrentUser(profileWindow.SelectedUser);
                
                // Set as main window and switch shutdown mode
                MainWindow = mainWindow;
                ShutdownMode = System.Windows.ShutdownMode.OnMainWindowClose;
                
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
        catch (System.Exception ex)
        {
            Log.Fatal(ex, "Application failed to start");
            System.Windows.MessageBox.Show($"Failed to start: {ex.Message}", "Error", 
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
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
        
        // AI Services
        services.AddSingleton<IAIProvider, GeminiProvider>();
        services.AddSingleton<IMotivationalMessageService, MotivationalMessageService>();


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

    /// <summary>
    /// Handle uncaught exceptions on the UI thread
    /// </summary>
    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled UI thread exception");
        
        System.Windows.MessageBox.Show(
            $"An unexpected error occurred:\n{e.Exception.Message}\n\nThe error has been logged.",
            "KeganOS Error",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Error);
        
        e.Handled = true; // Prevent app crash
    }

    /// <summary>
    /// Handle unobserved task exceptions (background tasks)
    /// </summary>
    private void OnUnobservedTaskException(object? sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unobserved task exception");
        e.SetObserved(); // Prevent app crash
    }

    /// <summary>
    /// Handle AppDomain unhandled exceptions (last resort)
    /// </summary>
    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as System.Exception;
        Log.Fatal(ex, "AppDomain unhandled exception (IsTerminating: {IsTerminating})", e.IsTerminating);
        
        if (e.IsTerminating)
        {
            System.Windows.MessageBox.Show(
                $"A fatal error occurred:\n{ex?.Message ?? "Unknown error"}\n\nThe application will now close.",
                "Fatal Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }
}
