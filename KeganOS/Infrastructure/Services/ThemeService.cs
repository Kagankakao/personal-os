using KeganOS.Core.Interfaces;
using KeganOS.Core.Models;
using Serilog;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Linq;

namespace KeganOS.Infrastructure.Services;

/// <summary>
/// Service for managing application themes
/// </summary>
public class ThemeService : IThemeService
{
    private readonly ILogger _logger = Log.ForContext<ThemeService>();
    private readonly string _kegomoDoroPath;
    private readonly string _appDataPath;
    private readonly string _themesFilePath;
    
    private List<Theme> _builtInThemes = [];
    
    public ThemeService()
    {
        // Path logic matching JournalService
        _kegomoDoroPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "kegomodoro");
        _appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KeganOS");
        _themesFilePath = Path.Combine(_appDataPath, "themes.json");
        
        InitializeBuiltInThemes();
    }
    
    private void InitializeBuiltInThemes()
    {
        _builtInThemes = new List<Theme>
        {
            new Theme
            {
                Id = "dark_default",
                Name = "Dark",
                Description = "Default dark theme",
                BackgroundColor = "#0D0D0D",
                AccentColor = "#FFFFFF",
                TextColor = "#FFFFFF",
                MainImagePath = "default_main.png",
                FloatingImagePath = "default_float.png",
                IsCustom = false,
                IsDark = true
            },
            new Theme
            {
                Id = "berserk",
                Name = "Berserk",
                Description = "Dark red theme inspired by Guts",
                BackgroundColor = "#0D0000",
                AccentColor = "#BB0000",
                TextColor = "#FFCCCC",
                MainImagePath = "berserk_main.png",
                FloatingImagePath = "berserk_float.png",
                IsCustom = false,
                IsDark = true
            },
            new Theme
            {
                Id = "tomato",
                Name = "Tomato",
                Description = "Light red theme for productivity",
                BackgroundColor = "#FFF5F5",
                AccentColor = "#FF4444",
                TextColor = "#330000",
                SecondaryTextColor = "#660000",
                MainImagePath = "tomato_main.png",
                FloatingImagePath = "tomato_float.png",
                IsCustom = false,
                IsDark = false
            },
            new Theme
            {
                Id = "forest",
                Name = "Forest",
                Description = "Calm green theme",
                BackgroundColor = "#051105",
                AccentColor = "#44AA44",
                TextColor = "#DDFFDD",
                 MainImagePath = "forest_main.png",
                FloatingImagePath = "forest_float.png",
                IsCustom = false,
                IsDark = true
            }
        };
    }

    public async Task<IEnumerable<Theme>> GetAvailableThemesAsync()
    {
        var themes = new List<Theme>(_builtInThemes);
        
        // Load custom themes
        if (File.Exists(_themesFilePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_themesFilePath);
                var customThemes = JsonSerializer.Deserialize<List<Theme>>(json);
                if (customThemes != null)
                {
                    themes.AddRange(customThemes);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load custom themes");
            }
        }
        
        return themes;
    }

    public Task<Theme> GetCurrentThemeAsync()
    {
        // In a real app, we'd read this from user preferences or config.csv
        // For now, return default
        return Task.FromResult(_builtInThemes.First());
    }

    public async Task<bool> ApplyThemeAsync(Theme theme)
    {
        try
        {
            _logger.Information("Applying theme: {Name} ({Id})", theme.Name, theme.Id);
            
            // 1. Update config.csv in KEGOMODORO
            var configPath = Path.Combine(_kegomoDoroPath, "dependencies", "texts", "Configurations", "configuration.csv");
            if (File.Exists(configPath))
            {
                var lines = await File.ReadAllLinesAsync(configPath);
                if (lines.Length > 0)
                {
                    var dataLines = new List<string>(lines);
                    var header = dataLines[0];
                    var values = dataLines.Count > 1 ? dataLines[1] : "";
                    
                    // Parse header to find indices or append
                    var headers = header.Split(',').ToList();
                    var valueList = values.Split(',').ToList();
                    
                    // Ensure values match headers length roughly (simple CSV)
                    while (valueList.Count < headers.Count) valueList.Add("");

                    // Helper to set or append column
                    void SetColumn(string colName, string colValue)
                    {
                        int idx = headers.IndexOf(colName);
                        if (idx == -1)
                        {
                            headers.Add(colName);
                            valueList.Add(colValue);
                        }
                        else
                        {
                            if (idx < valueList.Count)
                                valueList[idx] = colValue;
                            else
                                valueList.Add(colValue); // Should not happen if aligned
                        }
                    }
                    
                    // Map Theme Properties to KEGOMODORO constants (Logic matches main.py update)
                    // If IsDark is true: BLACK var (Text) = Theme.TextColor, WHITE var (Bg) = Theme.BackgroundColor
                    // If IsDark is false: BLACK var (Text) = Theme.TextColor (Black), WHITE var (Bg) = Theme.BackgroundColor (White)
                    // main.py logic: BLACK = THEME_TEXT, WHITE = THEME_BG
                    
                    SetColumn("THEME_TEXT", theme.TextColor);
                    SetColumn("THEME_BG", theme.BackgroundColor);
                    SetColumn("THEME_ACCENT", theme.AccentColor);
                    
                    // Reconstruct CSV
                    var sb = new StringBuilder();
                    sb.AppendLine(string.Join(",", headers));
                    sb.AppendLine(string.Join(",", valueList));
                    
                    await File.WriteAllTextAsync(configPath, sb.ToString());
                    _logger.Information("Updated configuration.csv");
                }
            }
            
            // 2. Copy images
            // Destination paths match main.py hardcoded paths
            var destMain = Path.Combine(_kegomoDoroPath, "dependencies", "images", "main_image.png");
            var destFloat = Path.Combine(_kegomoDoroPath, "dependencies", "images", "behelit.png");
            
            // Source paths - check specific theme folder first, else assume relative to Assets/Themes
            // NOTE: For built-in themes we might need to deploy valid files. this logic assumes they exist.
            // For now, checks absolute path.
            
            if (!string.IsNullOrEmpty(theme.MainImagePath) && File.Exists(theme.MainImagePath))
            {
                File.Copy(theme.MainImagePath, destMain, true);
            } 
            else 
            {
                // Try Assets path implementation (Pending asset creation)
                 _logger.Warning("Main image not found at {Path}", theme.MainImagePath);
            }

            if (!string.IsNullOrEmpty(theme.FloatingImagePath) && File.Exists(theme.FloatingImagePath))
            {
                File.Copy(theme.FloatingImagePath, destFloat, true);
            }

            _logger.Information("Theme applied successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to apply theme");
            return false;
        }
    }

    public async Task<bool> SaveCustomThemeAsync(Theme theme)
    {
        try
        {
            List<Theme> customThemes = [];
            if (File.Exists(_themesFilePath))
            {
                var json = await File.ReadAllTextAsync(_themesFilePath);
                customThemes = JsonSerializer.Deserialize<List<Theme>>(json) ?? [];
            }
            
            // Update or Add
            var existing = customThemes.FirstOrDefault(t => t.Id == theme.Id);
            if (existing != null)
            {
                customThemes.Remove(existing);
            }
            customThemes.Add(theme);
            
            var options = new JsonSerializerOptions { WriteIndented = true };
            await File.WriteAllTextAsync(_themesFilePath, JsonSerializer.Serialize(customThemes, options));
            
            _logger.Information("Custom theme saved: {Name}", theme.Name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to save custom theme");
            return false;
        }
    }

    public async Task<bool> DeleteThemeAsync(string themeId)
    {
        try
        {
            if (File.Exists(_themesFilePath))
            {
                var json = await File.ReadAllTextAsync(_themesFilePath);
                var customThemes = JsonSerializer.Deserialize<List<Theme>>(json) ?? [];
                
                var theme = customThemes.FirstOrDefault(t => t.Id == themeId);
                if (theme != null)
                {
                    customThemes.Remove(theme);
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    await File.WriteAllTextAsync(_themesFilePath, JsonSerializer.Serialize(customThemes, options));
                    _logger.Information("Custom theme deleted: {Id}", themeId);
                    return true;
                }
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to delete custom theme");
            return false;
        }
    }
}
