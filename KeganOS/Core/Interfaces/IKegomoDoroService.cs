using KeganOS.Core.Models;

namespace KeganOS.Core.Interfaces;

/// <summary>
/// Service for KEGOMODORO Python app integration
/// </summary>
public interface IKegomoDoroService
{
    /// <summary>
    /// Launch the KEGOMODORO Python application (legacy, uses default config)
    /// </summary>
    void Launch();
    
    /// <summary>
    /// Launch the KEGOMODORO Python application with user-specific configuration
    /// </summary>
    void Launch(User user);

    /// <summary>
    /// Check if KEGOMODORO is currently running
    /// </summary>
    bool IsRunning { get; }
    
    /// <summary>
    /// Check if any KEGOMODORO instance is running (including externally launched ones)
    /// </summary>
    bool IsAnyInstanceRunning { get; }
    
    /// <summary>
    /// Last error message if launch failed
    /// </summary>
    string? LastError { get; }

    /// <summary>
    /// Update timer configuration
    /// </summary>
    Task UpdateConfigurationAsync(int workMin, int shortBreak, int longBreak);

    /// <summary>
    /// Update theme/appearance settings
    /// </summary>
    Task UpdateThemeAsync(string backgroundColor, string? mainImagePath = null);

    /// <summary>
    /// Get the current configuration for a specific user
    /// </summary>
    Task<UserPreferences> GetConfigurationAsync(User? user = null);
}
