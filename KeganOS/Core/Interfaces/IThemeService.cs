using KeganOS.Core.Models;

namespace KeganOS.Core.Interfaces;

/// <summary>
/// Service for managing application themes
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// Get all available themes (built-in + custom)
    /// </summary>
    Task<IEnumerable<Theme>> GetAvailableThemesAsync();
    
    /// <summary>
    /// Get the currently active theme
    /// </summary>
    Task<Theme> GetCurrentThemeAsync();
    
    /// <summary>
    /// Apply a theme to KeganOS and update KEGOMODORO config
    /// </summary>
    Task<bool> ApplyThemeAsync(Theme theme);
    
    /// <summary>
    /// Save a new custom theme
    /// </summary>
    Task<bool> SaveCustomThemeAsync(Theme theme);
    
    /// <summary>
    /// Delete a custom theme
    /// </summary>
    Task<bool> DeleteThemeAsync(string themeId);
}
