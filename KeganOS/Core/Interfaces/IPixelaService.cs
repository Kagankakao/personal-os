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
    /// Register a new user on Pixe.la
    /// </summary>
    Task<bool> RegisterUserAsync(string username, string token);

    /// <summary>
    /// Create a new graph for tracking hours
    /// </summary>
    Task<bool> CreateGraphAsync(User user, string graphId, string graphName);
}
