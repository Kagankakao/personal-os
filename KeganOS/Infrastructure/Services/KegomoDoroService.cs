using KeganOS.Core.Interfaces;
using KeganOS.Core.Models;
using Serilog;
using System.Diagnostics;
using System.IO;

namespace KeganOS.Infrastructure.Services;

/// <summary>
/// Service for launching and configuring KEGOMODORO Python app
/// </summary>
public class KegomoDoroService : IKegomoDoroService
{
    private readonly ILogger _logger = Log.ForContext<KegomoDoroService>();
    private readonly string _kegomoDoroPath;
    private readonly string _configPath;
    private Process? _process;

    public KegomoDoroService()
    {
        // Relative to the application directory
        _kegomoDoroPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "kegomodoro");
        _configPath = Path.Combine(_kegomoDoroPath, "dependencies", "texts", "Configurations", "configuration.csv");
        
        _logger.Debug("KEGOMODORO path: {Path}", _kegomoDoroPath);
        _logger.Debug("Config path: {ConfigPath}", _configPath);
    }

    public bool IsRunning => _process != null && !_process.HasExited;

    public void Launch()
    {
        _logger.Information("Launching KEGOMODORO...");
        
        try
        {
            var mainPyPath = Path.Combine(_kegomoDoroPath, "main.py");
            
            if (!File.Exists(mainPyPath))
            {
                _logger.Error("KEGOMODORO main.py not found at {Path}", mainPyPath);
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"\"{mainPyPath}\"",
                WorkingDirectory = _kegomoDoroPath,
                UseShellExecute = false,
                CreateNoWindow = false
            };

            _process = Process.Start(startInfo);
            _logger.Information("KEGOMODORO launched successfully (PID: {PID})", _process?.Id);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to launch KEGOMODORO");
        }
    }

    public async Task UpdateConfigurationAsync(int workMin, int shortBreak, int longBreak)
    {
        _logger.Information("Updating KEGOMODORO configuration: Work={Work}min, ShortBreak={Short}min, LongBreak={Long}min",
            workMin, shortBreak, longBreak);

        try
        {
            var config = await GetConfigurationAsync();
            
            var lines = new[]
            {
                "WORK_MIN,SHORT_BREAK_MIN,LONG_BREAK_MIN,NOTEPAD_MODE",
                $"{workMin},{shortBreak},{longBreak},{(config.KegomoDoroWorkMin > 0 ? "FALSE" : "FALSE")}"
            };

            await File.WriteAllLinesAsync(_configPath, lines);
            _logger.Information("Configuration updated successfully");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to update configuration");
            throw;
        }
    }

    public async Task UpdateThemeAsync(string backgroundColor, string? mainImagePath = null)
    {
        _logger.Information("Updating KEGOMODORO theme: BgColor={Color}, Image={Image}", 
            backgroundColor, mainImagePath ?? "default");

        // TODO: Update Python code to read these from config
        // For now, log the intent
        await Task.CompletedTask;
    }

    public async Task<UserPreferences> GetConfigurationAsync()
    {
        _logger.Debug("Reading KEGOMODORO configuration...");
        
        var prefs = new UserPreferences();

        try
        {
            if (File.Exists(_configPath))
            {
                var lines = await File.ReadAllLinesAsync(_configPath);
                if (lines.Length >= 2)
                {
                    var values = lines[1].Split(',');
                    if (values.Length >= 3)
                    {
                        prefs.KegomoDoroWorkMin = int.TryParse(values[0], out var work) ? work : 25;
                        prefs.KegomoDoroShortBreak = int.TryParse(values[1], out var shortB) ? shortB : 5;
                        prefs.KegomoDoroLongBreak = int.TryParse(values[2], out var longB) ? longB : 20;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to read configuration");
        }

        return prefs;
    }
}
