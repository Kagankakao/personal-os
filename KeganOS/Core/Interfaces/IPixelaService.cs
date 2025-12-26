using KeganOS.Core.Models;

namespace KeganOS.Core.Interfaces;

/// <summary>
/// Service for Pixe.la habit tracking integration
/// </summary>
public interface IPixelaService
{
    /// <summary>
    /// Check if Pixe.la is configured for the user
    /// </summary>
    bool IsConfigured(User user);

    /// <summary>
    /// Get statistics for the user's graph
    /// </summary>
    Task<PixelaStats?> GetStatsAsync(User user);

    /// <summary>
    /// Get pixel data for heatmap visualization
    /// </summary>
    Task<IEnumerable<PixelaPixel>> GetPixelsAsync(User user, DateTime? from = null, DateTime? to = null);

    /// <summary>
    /// Get the graph definition (name, color, unit) from Pixe.la
    /// </summary>
    Task<PixelaGraphDefinition?> GetGraphDefinitionAsync(User user);

    /// <summary>
    /// Generate a token from username (for auto-registration)
    /// </summary>
    string GenerateToken(string username);

    /// <summary>
    /// Validate username format
    /// Returns: (isValid, errorMessage)
    /// </summary>
    Task<(bool isAvailable, string? error)> CheckUsernameAvailabilityAsync(string username);

    /// <summary>
    /// Register a new user on Pixe.la with retry loop (free version rate-limited)
    /// Returns: (success, errorMessage)
    /// </summary>
    Task<(bool success, string? error)> RegisterUserAsync(string username, string token);

    /// <summary>
    /// Create a new graph for tracking hours
    /// </summary>
    Task<bool> CreateGraphAsync(User user, string graphId, string graphName);

    /// <summary>
    /// Enable PNG format for an existing graph
    /// </summary>
    Task<bool> EnablePngAsync(User user);

    /// <summary>
    /// Get quantity for a specific date
    /// </summary>
    Task<double> GetPixelByDateAsync(User user, DateTime date);

    /// <summary>
    /// Get raw SVG content of the graph
    /// </summary>
    Task<string> GetSvgAsync(User user, string? date = null, string? appearance = "dark");

    /// <summary>
    /// Get the date of the most recent non-zero pixel record
    /// </summary>
    Task<string?> GetLatestActiveDateAsync(User user);

    /// <summary>
    /// Post or update a pixel value for a specific date
    /// Uses PUT /v1/users/{username}/graphs/{graphID}/{yyyyMMdd}
    /// </summary>
    Task<bool> PostPixelAsync(User user, DateTime date, double quantity);

    /// <summary>
    /// Increment pixel value for a specific date
    /// Uses PUT /v1/users/{username}/graphs/{graphID}/{yyyyMMdd}/increment
    /// </summary>
    Task<bool> IncrementPixelAsync(User user, DateTime date, double quantity);

    /// <summary>
    /// Update graph settings on Pixe.la
    /// Uses PUT /v1/users/{username}/graphs/{graphID}
    /// Returns: (success, errorMessage)
    /// </summary>
    Task<(bool success, string? error)> UpdateGraphAsync(
        User user, 
        string? name = null, 
        string? color = null, 
        string? unit = null, 
        bool? isEnablePng = null,
        bool? startOnMonday = null);
}
