using KeganOS.Core.Interfaces;
using KeganOS.Core.Models;
using KeganOS.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Serilog;

namespace KeganOS.Infrastructure.Services;

/// <summary>
/// SQLite-backed user service implementation
/// </summary>
public class UserService : IUserService
{
    private readonly ILogger _logger = Log.ForContext<UserService>();
    private readonly AppDbContext _db;

    public UserService(AppDbContext db)
    {
        _db = db;
        _logger.Debug("UserService initialized");
    }

    public async Task<IEnumerable<User>> GetAllUsersAsync()
    {
        _logger.Debug("Fetching all users...");
        var users = new List<User>();

        using var conn = _db.GetConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Users ORDER BY LastLoginAt DESC";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            users.Add(MapUser(reader));
        }

        _logger.Information("Found {Count} users", users.Count);
        return users;
    }

    public async Task<User?> GetUserByIdAsync(int id)
    {
        _logger.Debug("Fetching user {Id}...", id);

        using var conn = _db.GetConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Users WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapUser(reader);
        }

        _logger.Warning("User {Id} not found", id);
        return null;
    }

    public async Task<User> CreateUserAsync(User user)
    {
        _logger.Information("Creating user: {Name}", user.DisplayName);

        using var conn = _db.GetConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Users (DisplayName, PersonalSymbol, AvatarPath, JournalFileName, PixelaUsername, PixelaToken, PixelaGraphId, GeminiApiKey)
            VALUES (@name, @symbol, @avatar, @journal, @pxUser, @pxToken, @pxGraph, @gemini);
            SELECT last_insert_rowid();";

        cmd.Parameters.AddWithValue("@name", user.DisplayName);
        cmd.Parameters.AddWithValue("@symbol", user.PersonalSymbol ?? "");
        cmd.Parameters.AddWithValue("@avatar", user.AvatarPath ?? "");
        cmd.Parameters.AddWithValue("@journal", user.JournalFileName);
        cmd.Parameters.AddWithValue("@pxUser", user.PixelaUsername ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@pxToken", user.PixelaToken ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@pxGraph", user.PixelaGraphId ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@gemini", user.GeminiApiKey ?? (object)DBNull.Value);

        var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        user.Id = id;

        // Create default preferences
        await CreateDefaultPreferencesAsync(conn, id);

        _logger.Information("Created user {Id}: {Name}", id, user.DisplayName);
        return user;
    }

    private async Task CreateDefaultPreferencesAsync(SqliteConnection conn, int userId)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO UserPreferences (UserId) VALUES (@userId)";
        cmd.Parameters.AddWithValue("@userId", userId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateUserAsync(User user)
    {
        _logger.Information("Updating user {Id}: {Name} (XP={XP}, Hours={Hours})", user.Id, user.DisplayName, user.XP, user.TotalHours);

        using var conn = _db.GetConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE Users SET 
                DisplayName = @name,
                PersonalSymbol = @symbol,
                AvatarPath = @avatar,
                JournalFileName = @journal,
                PixelaUsername = @pxUser,
                PixelaToken = @pxToken,
                PixelaGraphId = @pxGraph,
                GeminiApiKey = @gemini,
                TotalHours = @totalHours,
                XP = @xp,
                UnlockedAchievements = @achievements,
                SavedColors = @savedColors,
                LastLoginAt = CURRENT_TIMESTAMP
            WHERE Id = @id";

        cmd.Parameters.AddWithValue("@id", user.Id);
        cmd.Parameters.AddWithValue("@name", user.DisplayName);
        cmd.Parameters.AddWithValue("@symbol", user.PersonalSymbol ?? "");
        cmd.Parameters.AddWithValue("@avatar", user.AvatarPath ?? "");
        cmd.Parameters.AddWithValue("@journal", user.JournalFileName);
        cmd.Parameters.AddWithValue("@pxUser", user.PixelaUsername ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@pxToken", user.PixelaToken ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@pxGraph", user.PixelaGraphId ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@gemini", user.GeminiApiKey ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@totalHours", user.TotalHours);
        cmd.Parameters.AddWithValue("@xp", user.XP);
        cmd.Parameters.AddWithValue("@achievements", user.UnlockedAchievementsJson ?? "");
        cmd.Parameters.AddWithValue("@savedColors", user.SavedColors ?? "");

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteUserAsync(int id)
    {
        _logger.Warning("Deleting user {Id}", id);

        using var conn = _db.GetConnection();
        
        // Delete preferences first
        var prefCmd = conn.CreateCommand();
        prefCmd.CommandText = "DELETE FROM UserPreferences WHERE UserId = @id";
        prefCmd.Parameters.AddWithValue("@id", id);
        await prefCmd.ExecuteNonQueryAsync();

        // Delete user
        var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Users WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<UserPreferences> GetPreferencesAsync(int userId)
    {
        using var conn = _db.GetConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM UserPreferences WHERE UserId = @userId";
        cmd.Parameters.AddWithValue("@userId", userId);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new UserPreferences
            {
                UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
                ThemeName = reader.GetString(reader.GetOrdinal("ThemeName")),
                AccentColor = reader.GetString(reader.GetOrdinal("AccentColor")),
                KegomoDoroWorkMin = reader.GetInt32(reader.GetOrdinal("KegomoDoroWorkMin")),
                KegomoDoroShortBreak = reader.GetInt32(reader.GetOrdinal("KegomoDoroShortBreak")),
                KegomoDoroLongBreak = reader.GetInt32(reader.GetOrdinal("KegomoDoroLongBreak")),
                KegomoDoroBackgroundColor = reader.GetString(reader.GetOrdinal("KegomoDoroBackgroundColor")),
                KegomoDoroMainImagePath = reader.IsDBNull(reader.GetOrdinal("KegomoDoroMainImagePath")) 
                    ? null : reader.GetString(reader.GetOrdinal("KegomoDoroMainImagePath"))
            };
        }

        return new UserPreferences { UserId = userId };
    }

    public async Task UpdatePreferencesAsync(UserPreferences prefs)
    {
        using var conn = _db.GetConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE UserPreferences SET 
                ThemeName = @theme,
                AccentColor = @accent,
                KegomoDoroWorkMin = @work,
                KegomoDoroShortBreak = @shortB,
                KegomoDoroLongBreak = @longB,
                KegomoDoroBackgroundColor = @bgColor,
                KegomoDoroMainImagePath = @imgPath
            WHERE UserId = @userId";

        cmd.Parameters.AddWithValue("@userId", prefs.UserId);
        cmd.Parameters.AddWithValue("@theme", prefs.ThemeName);
        cmd.Parameters.AddWithValue("@accent", prefs.AccentColor);
        cmd.Parameters.AddWithValue("@work", prefs.KegomoDoroWorkMin);
        cmd.Parameters.AddWithValue("@shortB", prefs.KegomoDoroShortBreak);
        cmd.Parameters.AddWithValue("@longB", prefs.KegomoDoroLongBreak);
        cmd.Parameters.AddWithValue("@bgColor", prefs.KegomoDoroBackgroundColor);
        cmd.Parameters.AddWithValue("@imgPath", prefs.KegomoDoroMainImagePath ?? (object)DBNull.Value);

        await cmd.ExecuteNonQueryAsync();
    }

    private static User MapUser(SqliteDataReader reader)
    {
        // Try to get TotalHours, default to 0 if column doesn't exist (migration)
        double totalHours = 0;
        long xp = 0;
        string unlockedAchievements = "";
        
        try
        {
            var ordinal = reader.GetOrdinal("TotalHours");
            if (!reader.IsDBNull(ordinal))
                totalHours = reader.GetDouble(ordinal);
        }
        catch { /* Column may not exist in old DB */ }
        
        try
        {
            var ordinal = reader.GetOrdinal("XP");
            if (!reader.IsDBNull(ordinal))
                xp = reader.GetInt64(ordinal);
        }
        catch { /* Column may not exist in old DB */ }
        
        try
        {
            var ordinal = reader.GetOrdinal("UnlockedAchievements");
            if (!reader.IsDBNull(ordinal))
                unlockedAchievements = reader.GetString(ordinal);
        }
        catch { /* Column may not exist in old DB */ }
        
        string savedColors = "";
        try
        {
            var ordinal = reader.GetOrdinal("SavedColors");
            if (!reader.IsDBNull(ordinal))
                savedColors = reader.GetString(ordinal);
        }
        catch { /* Column may not exist in old DB */ }
        
        return new User
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            DisplayName = reader.GetString(reader.GetOrdinal("DisplayName")),
            PersonalSymbol = reader.IsDBNull(reader.GetOrdinal("PersonalSymbol")) ? "" : reader.GetString(reader.GetOrdinal("PersonalSymbol")),
            AvatarPath = reader.IsDBNull(reader.GetOrdinal("AvatarPath")) ? "" : reader.GetString(reader.GetOrdinal("AvatarPath")),
            JournalFileName = reader.GetString(reader.GetOrdinal("JournalFileName")),
            PixelaUsername = reader.IsDBNull(reader.GetOrdinal("PixelaUsername")) ? null : reader.GetString(reader.GetOrdinal("PixelaUsername")),
            PixelaToken = reader.IsDBNull(reader.GetOrdinal("PixelaToken")) ? null : reader.GetString(reader.GetOrdinal("PixelaToken")),
            PixelaGraphId = reader.IsDBNull(reader.GetOrdinal("PixelaGraphId")) ? null : reader.GetString(reader.GetOrdinal("PixelaGraphId")),
            GeminiApiKey = reader.IsDBNull(reader.GetOrdinal("GeminiApiKey")) ? null : reader.GetString(reader.GetOrdinal("GeminiApiKey")),
            TotalHours = totalHours,
            XP = xp,
            UnlockedAchievementsJson = unlockedAchievements,
            SavedColors = savedColors,
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            LastLoginAt = reader.GetDateTime(reader.GetOrdinal("LastLoginAt"))
        };
    }
}
