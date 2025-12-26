using KeganOS.Core.Models;

namespace KeganOS.Core.Interfaces;

/// <summary>
/// Service for user profile management
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Get all user profiles
    /// </summary>
    Task<IEnumerable<User>> GetAllUsersAsync();

    /// <summary>
    /// Get a user by ID
    /// </summary>
    Task<User?> GetUserByIdAsync(int id);

    /// <summary>
    /// Create a new user profile
    /// </summary>
    Task<User> CreateUserAsync(User user);

    /// <summary>
    /// Update an existing user profile
    /// </summary>
    Task UpdateUserAsync(User user);

    /// <summary>
    /// Delete a user profile
    /// </summary>
    Task DeleteUserAsync(int id);

    /// <summary>
    /// Get user preferences
    /// </summary>
    Task<UserPreferences> GetPreferencesAsync(int userId);

    /// <summary>
    /// Update user preferences
    /// </summary>
    Task UpdatePreferencesAsync(UserPreferences preferences);

    /// <summary>
    /// Set the last active user ID for session persistence
    /// </summary>
    Task SetLastActiveUserIdAsync(int? userId);

    /// <summary>
    /// Get the last active user ID from the database
    /// </summary>
    Task<int?> GetLastActiveUserIdAsync();
}
