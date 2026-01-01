using KeganOS.Core.Interfaces;
using KeganOS.Core.Models;
using Serilog;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

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

    public bool IsRunning 
    {
        get
        {
            try
            {
                if (_process == null)
                {
                    return false;
                }
                
                // Refresh process info
                _process.Refresh();
                
                if (_process.HasExited)
                {
                    _logger.Debug("Tracked KEGOMODORO process has exited, clearing reference");
                    _process = null;
                    return false;
                }
                
                _logger.Debug("Tracked KEGOMODORO process is still running (PID: {PID})", _process.Id);
                return true;
            }
            catch (Exception ex)
            {
                // Process object may be invalid
                _logger.Debug(ex, "Error checking tracked process, clearing reference");
                _process = null;
                return false;
            }
        }
    }
    
    /// <summary>
    /// Check if any KEGOMODORO process is running (even ones started externally)
    /// Uses lock file mechanism - KEGOMODORO creates .kegomodoro.lock when running
    /// Also validates that the PID in the lock file is actually running
    /// </summary>
    public bool IsAnyInstanceRunning
    {
        get
        {
            // Check our tracked process first
            if (IsRunning)
            {
                _logger.Information("IsAnyInstanceRunning: TRUE (tracked process)");
                return true;
            }
            
            // Check for lock file created by KEGOMODORO
            try
            {
                var lockFilePath = Path.Combine(_kegomoDoroPath, ".kegomodoro.lock");
                if (File.Exists(lockFilePath))
                {
                    // Read the PID and verify the process is actually running
                    var content = File.ReadAllText(lockFilePath).Trim();
                    
                    // Handle "launching" marker
                    if (content == "launching")
                    {
                        _logger.Debug("Lock file contains 'launching' marker - assuming KEGOMODORO is starting");
                        return true;
                    }
                    
                    if (int.TryParse(content, out int pid))
                    {
                        // Check if process with this PID exists
                        try
                        {
                            var process = System.Diagnostics.Process.GetProcessById(pid);
                            // Process exists - but is it actually KEGOMODORO?
                            if (process != null && !process.HasExited)
                            {
                                _logger.Debug("Lock file PID {PID} is running", pid);
                                return true;
                            }
                        }
                        catch (ArgumentException)
                        {
                            // Process doesn't exist - stale lock file
                            _logger.Debug("Lock file PID {PID} not found - stale lock file, deleting", pid);
                            try { File.Delete(lockFilePath); } catch { }
                        }
                        catch (InvalidOperationException)
                        {
                            // Process has exited
                            _logger.Debug("Lock file PID {PID} has exited - stale lock file, deleting", pid);
                            try { File.Delete(lockFilePath); } catch { }
                        }
                    }
                    else
                    {
                        _logger.Debug("Lock file contains invalid content: {Content}", content);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Error checking for KEGOMODORO lock file");
            }
            
            _logger.Debug("IsAnyInstanceRunning: FALSE (no process, no valid lock file)");
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

    /// <summary>
    /// Launch KEGOMODORO with user-specific configuration
    /// Writes user_config.json before launching so KEGOMODORO can read user-specific settings
    /// </summary>
    public void Launch(User user)
    {
        _logger.Information("Preparing to launch KEGOMODORO for user: {User}", user.DisplayName);
        
        try
        {
            // Create user-specific data folder
            var userDataFolder = Path.Combine(_kegomoDoroPath, "dependencies", "texts", "Users", user.DisplayName);
            Directory.CreateDirectory(userDataFolder);
            
            // Write user_config.json for KEGOMODORO to read
            var userConfigPath = Path.Combine(_kegomoDoroPath, "user_config.json");
            var userConfig = new
            {
                username = user.DisplayName.Trim(),
                pixela_username = (user.PixelaUsername ?? "").Trim(),
                pixela_token = (user.PixelaToken ?? "").Trim(),
                pixela_graph_id = (user.PixelaGraphId ?? "").Trim(),
                pixela_color = (user.PixelaGraphColor ?? "shibafu").Trim(),
                data_folder = $"dependencies/texts/Users/{user.DisplayName}/",
                journey_file = user.JournalFileName ?? $"KA\u00c6[\u00c6\u00df#.txt"
            };
            
            var json = JsonSerializer.Serialize(userConfig, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(userConfigPath, json);
            _logger.Information("Written user_config.json for {User}", user.DisplayName);
            
            // Also copy time.csv to user folder if it doesn't exist there
            var userTimeCsvPath = Path.Combine(userDataFolder, "time.csv");
            if (!File.Exists(userTimeCsvPath))
            {
                var defaultTimeCsv = "hours,minute,second\n0,0,0\n";
                File.WriteAllText(userTimeCsvPath, defaultTimeCsv);
                _logger.Debug("Created default time.csv for user {User}", user.DisplayName);
            }
            
            // Copy configuration.csv to user folder if it doesn't exist
            var userConfigCsvPath = Path.Combine(userDataFolder, "configuration.csv");
            if (!File.Exists(userConfigCsvPath) && File.Exists(_configPath))
            {
                File.Copy(_configPath, userConfigCsvPath);
                _logger.Debug("Copied default configuration to user folder for {User}", user.DisplayName);
            }
            
            // Copy journey file to user folder if it doesn't exist
            var journeyFileName = user.JournalFileName ?? "KAÆ[Æß#.txt";
            var userJourneyPath = Path.Combine(userDataFolder, journeyFileName);
            if (!File.Exists(userJourneyPath))
            {
                // Try to find and copy existing global journey file
                var globalTextsPath = Path.Combine(_kegomoDoroPath, "dependencies", "texts");
                var globalJourneyPath = Path.Combine(globalTextsPath, journeyFileName);
                
                if (File.Exists(globalJourneyPath))
                {
                    File.Copy(globalJourneyPath, userJourneyPath);
                    _logger.Information("Copied journey file to user folder for {User}", user.DisplayName);
                }
                else
                {
                    // Find any .txt file in global texts
                    var txtFiles = Directory.GetFiles(globalTextsPath, "*.txt")
                        .Where(f => !f.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    
                    if (txtFiles.Count > 0)
                    {
                        File.Copy(txtFiles[0], userJourneyPath);
                        _logger.Information("Copied existing journey file {From} to user folder for {User}", 
                            Path.GetFileName(txtFiles[0]), user.DisplayName);
                    }
                    else
                    {
                        // Create empty journey file
                        File.WriteAllText(userJourneyPath, "");
                        _logger.Debug("Created empty journey file for user {User}", user.DisplayName);
                    }
                }
            }
            
            // Create user-specific images folder and copy defaults
            var userImagesFolder = Path.Combine(_kegomoDoroPath, "dependencies", "images", "Users", user.DisplayName);
            Directory.CreateDirectory(userImagesFolder);
            
            var globalImagesFolder = Path.Combine(_kegomoDoroPath, "dependencies", "images");
            var imagesToCopy = new[] { "main_image.png", "behelit.png" };
            
            foreach (var imageName in imagesToCopy)
            {
                var userImagePath = Path.Combine(userImagesFolder, imageName);
                var globalImagePath = Path.Combine(globalImagesFolder, imageName);
                
                if (!File.Exists(userImagePath) && File.Exists(globalImagePath))
                {
                    File.Copy(globalImagePath, userImagePath);
                    _logger.Debug("Copied {Image} to user images folder for {User}", imageName, user.DisplayName);
                }
            }
            
            // Create user-specific audios folder and copy defaults
            var userAudiosFolder = Path.Combine(_kegomoDoroPath, "dependencies", "audios", "Users", user.DisplayName);
            Directory.CreateDirectory(userAudiosFolder);
            
            var globalAudiosFolder = Path.Combine(_kegomoDoroPath, "dependencies", "audios");
            var audiosToCopy = new[] { "new_work.mp3", "work.mp3", "short_break.mp3", "long_break.mp3" };
            
            foreach (var audioName in audiosToCopy)
            {
                var userAudioPath = Path.Combine(userAudiosFolder, audioName);
                var globalAudioPath = Path.Combine(globalAudiosFolder, audioName);
                
                if (!File.Exists(userAudioPath) && File.Exists(globalAudioPath))
                {
                    File.Copy(globalAudioPath, userAudioPath);
                    _logger.Debug("Copied {Audio} to user audios folder for {User}", audioName, user.DisplayName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to prepare user-specific config, launching with defaults");
        }
        
        // Now launch using the existing method
        Launch();
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

    public async Task<UserPreferences> GetConfigurationAsync(User? user = null)
    {
        _logger.Debug("Reading KEGOMODORO configuration...");
        
        var prefs = new UserPreferences();

        // Determine config path - use user-specific folder if user provided
        string configPath = _configPath;
        if (user != null)
        {
            var userConfigPath = Path.Combine(_kegomoDoroPath, "dependencies", "texts", "Users", user.DisplayName, "configuration.csv");
            if (File.Exists(userConfigPath))
            {
                configPath = userConfigPath;
                _logger.Debug("Using user-specific config: {Path}", userConfigPath);
            }
        }

        try
        {
            if (File.Exists(configPath))
            {
                var lines = await File.ReadAllLinesAsync(configPath);
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
