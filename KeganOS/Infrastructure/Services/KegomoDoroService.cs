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
    private string? _lastError;

    public KegomoDoroService()
    {
        // Try multiple possible locations for kegomodoro folder
        var possiblePaths = new[]
        {
            // From bin folder: go up to project root, then to kegomodoro
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "kegomodoro")),
            // From solution folder
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "kegomodoro")),
            // Direct sibling folder (if running from KeganOS project)
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "kegomodoro")),
            // User's projects folder - try to find it
            @"C:\Users\ariba\OneDrive\Documenti\Software Projects\AI Projects\personal-os\personal-os\kegomodoro"
        };

        _kegomoDoroPath = "";
        foreach (var path in possiblePaths)
        {
            var mainPy = Path.Combine(path, "main.py");
            _logger.Debug("Checking for KEGOMODORO at: {Path}", path);
            if (File.Exists(mainPy))
            {
                _kegomoDoroPath = path;
                _logger.Information("Found KEGOMODORO at: {Path}", path);
                break;
            }
        }

        if (string.IsNullOrEmpty(_kegomoDoroPath))
        {
            _logger.Error("KEGOMODORO not found in any expected location");
            _kegomoDoroPath = possiblePaths[0]; // Use first as default
        }

        _configPath = Path.Combine(_kegomoDoroPath, "dependencies", "texts", "Configurations", "configuration.csv");
        
        _logger.Debug("KEGOMODORO path: {Path}", _kegomoDoroPath);
        _logger.Debug("Config path: {ConfigPath}", _configPath);
    }

    public bool IsRunning => _process != null && !_process.HasExited;
    
    /// <summary>
    /// Check if any KEGOMODORO process is running (even ones started externally)
    /// Uses lock file mechanism - KEGOMODORO creates .kegomodoro.lock when running
    /// </summary>
    public bool IsAnyInstanceRunning
    {
        get
        {
            // Check our tracked process first
            if (IsRunning) return true;
            
            // Check for lock file created by KEGOMODORO
            try
            {
                var lockFilePath = Path.Combine(_kegomoDoroPath, ".kegomodoro.lock");
                if (File.Exists(lockFilePath))
                {
                    _logger.Debug("Found KEGOMODORO lock file at {Path}", lockFilePath);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Error checking for KEGOMODORO lock file");
            }
            
            return false;
        }
    }
    
    public string? LastError => _lastError;

    private string LockFilePath => Path.Combine(_kegomoDoroPath, ".kegomodoro.lock");

    public void Launch()
    {
        // Prevent multiple instances - check for ANY kegomodoro process
        if (IsAnyInstanceRunning)
        {
            _lastError = "KEGOMODORO is already running";
            _logger.Warning("KEGOMODORO is already running, not launching another instance");
            return;
        }

        _logger.Information("Launching KEGOMODORO...");
        _lastError = null;
        
        try
        {
            var mainPyPath = Path.Combine(_kegomoDoroPath, "main.py");
            
            if (!File.Exists(mainPyPath))
            {
                _lastError = $"main.py not found at:\n{mainPyPath}";
                _logger.Error("KEGOMODORO main.py not found at {Path}", mainPyPath);
                return;
            }

            // Create lock file IMMEDIATELY to prevent race condition with multiple clicks
            try
            {
                File.WriteAllText(LockFilePath, "launching");
                _logger.Debug("Created lock file at {Path}", LockFilePath);
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to create lock file");
            }

            _logger.Information("Launching from: {Path}", mainPyPath);

            // Launch Python with hidden console window
            var startInfo = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"\"{mainPyPath}\"",
                WorkingDirectory = _kegomoDoroPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            };

            _process = Process.Start(startInfo);
            
            if (_process != null)
            {
                _logger.Information("KEGOMODORO launched successfully (PID: {PID})", _process.Id);
            }
            else
            {
                _lastError = "Process.Start returned null";
                _logger.Error("Failed to start KEGOMODORO process");
            }
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 2)
        {
            _lastError = "Python not found. Make sure Python is installed and in your PATH.";
            _logger.Error(ex, "Python not found in PATH");
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
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
