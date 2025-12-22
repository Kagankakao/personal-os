using KeganOS.Core.Models;

namespace KeganOS.Core.Interfaces;

/// <summary>
/// Service for KEGOMODORO Python app integration
/// </summary>
public interface IKegomoDoroService
{
    /// <summary>
    /// Launch the KEGOMODORO Python application
    /// </summary>
    void Launch();

    /// <summary>
    /// Check if KEGOMODORO is currently running
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Update timer configuration
    /// </summary>
    Task UpdateConfigurationAsync(int workMin, int shortBreak, int longBreak);

    /// <summary>
    /// Update theme/appearance settings
    /// </summary>
    Task UpdateThemeAsync(string backgroundColor, string? mainImagePath = null);

    /// <summary>
    /// Get the current configuration
    /// </summary>
    Task<UserPreferences> GetConfigurationAsync();
}
