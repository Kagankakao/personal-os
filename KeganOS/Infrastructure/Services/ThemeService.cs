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
    
    private readonly IPixelaService? _pixelaService;
    
    public ThemeService(IPixelaService? pixelaService = null)
    {
        _pixelaService = pixelaService;
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
                PixelaColor = "kuro",
                IsCustom = false,
                IsDark = true
            },
            new Theme
            {
                Id = "berserk",
                Name = "Berserk",
                Description = "Guts' iconic theme. Dark red background with orange accent.",
                BackgroundColor = "#8B0000",
                AccentColor = "#EB5B00",
                TextColor = "#feffff",
                MainImagePath = "berserk_main.png",
                FloatingImagePath = "berserk_float.png",
                PixelaColor = "momiji",
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
                PixelaColor = "momiji",
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
                PixelaColor = "shibafu",
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

    public async Task<bool> ApplyThemeAsync(Theme theme, User? user = null)
    {
        try
        {
            _logger.Information("Applying theme: {Name} ({Id}) for user: {User}", 
                theme.Name, theme.Id, user?.DisplayName ?? "global");
            
            // Determine config path - user-specific or global
            string configPath;
            if (user != null)
            {
                var userConfigPath = Path.Combine(_kegomoDoroPath, "dependencies", "texts", "Users", user.DisplayName, "configuration.csv");
                if (File.Exists(userConfigPath))
                {
                    configPath = userConfigPath;
                }
                else
                {
                    configPath = Path.Combine(_kegomoDoroPath, "dependencies", "texts", "Configurations", "configuration.csv");
                }
            }
            else
            {
                configPath = Path.Combine(_kegomoDoroPath, "dependencies", "texts", "Configurations", "configuration.csv");
            }
            
            // 1. Update config.csv in KEGOMODORO
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
                    
                    SetColumn("THEME_TEXT", theme.TextColor);
                    SetColumn("THEME_BG", theme.BackgroundColor);
                    SetColumn("THEME_ACCENT", theme.AccentColor);
                    SetColumn("PIXELA_COLOR", theme.PixelaColor);
                    
                    // Reconstruct CSV
                    var sb = new StringBuilder();
                    sb.AppendLine(string.Join(",", headers));
                    sb.AppendLine(string.Join(",", valueList));
                    
                    await File.WriteAllTextAsync(configPath, sb.ToString());
                    _logger.Information("Updated configuration.csv");
                }
            }
            
            // 2. Sync with Pixe.la if service and user available
            if (_pixelaService != null && user != null && !string.IsNullOrEmpty(theme.PixelaColor))
            {
                _logger.Information("Syncing theme color to Pixe.la: {Color}", theme.PixelaColor);
                var (pixelaSuccess, pixelaError) = await _pixelaService.UpdateGraphAsync(user, color: theme.PixelaColor);
                if (!pixelaSuccess)
                {
                    _logger.Warning("Failed to sync theme to Pixe.la: {Error}", pixelaError);
                    // We don't fail the whole operation if only Pixela sync fails
                }
            }
            
            // 3. Copy images - to user folder if user provided, else global
            string destMain, destFloat;
            string assetsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Themes");
            
            // Fallback for development if bin/Assets doesn't exist
            if (!Directory.Exists(assetsPath))
            {
                assetsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Assets", "Themes");
            }
            
            if (user != null)
            {
                var userImagesFolder = Path.Combine(_kegomoDoroPath, "dependencies", "images", "Users", user.DisplayName);
                Directory.CreateDirectory(userImagesFolder);
                destMain = Path.Combine(userImagesFolder, "main_image.png");
                destFloat = Path.Combine(userImagesFolder, "behelit.png");
            }
            else
            {
                destMain = Path.Combine(_kegomoDoroPath, "dependencies", "images", "main_image.png");
                destFloat = Path.Combine(_kegomoDoroPath, "dependencies", "images", "behelit.png");
            }
            
            // Source paths
            var sourceMain = Path.Combine(assetsPath, theme.MainImagePath);
            var sourceFloat = Path.Combine(assetsPath, theme.FloatingImagePath);
            
            if (File.Exists(sourceMain))
            {
                _logger.Debug("Copying theme image: {Source} to {Dest}", sourceMain, destMain);
                File.Copy(sourceMain, destMain, true);
            } 
            else 
            {
                 _logger.Warning("Theme source image not found: {Path}", sourceMain);
            }

            if (File.Exists(sourceFloat))
            {
                _logger.Debug("Copying theme float: {Source} to {Dest}", sourceFloat, destFloat);
                File.Copy(sourceFloat, destFloat, true);
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
