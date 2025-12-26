using Microsoft.Data.Sqlite;
using Serilog;

namespace KeganOS.Infrastructure.Data;

/// <summary>
/// SQLite database context for KeganOS
/// </summary>
public class AppDbContext
{
    private readonly string _connectionString;
    private readonly ILogger _logger = Log.ForContext<AppDbContext>();
    private bool _initialized = false;

    public AppDbContext(string databasePath = "keganos.db")
    {
        _connectionString = $"Data Source={databasePath}";
        _logger.Debug("Database path: {DatabasePath}", databasePath);
    }

    /// <summary>
    /// Initialize the database (create tables if not exist)
    /// </summary>
    public void Initialize()
    {
        if (_initialized) return;
        
        _logger.Information("Initializing database...");
        
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            -- Users table
            CREATE TABLE IF NOT EXISTS Users (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                DisplayName TEXT NOT NULL,
                PersonalSymbol TEXT DEFAULT '',
                AvatarPath TEXT DEFAULT '',
                JournalFileName TEXT NOT NULL,
                PixelaUsername TEXT,
                PixelaToken TEXT,
                PixelaGraphId TEXT,
                GeminiApiKey TEXT,
                TotalHours REAL DEFAULT 0,
                XP INTEGER DEFAULT 0,
                UnlockedAchievements TEXT,
                CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                LastLoginAt DATETIME DEFAULT CURRENT_TIMESTAMP
            );

            -- Journal entries (synced from text file)
            CREATE TABLE IF NOT EXISTS JournalEntries (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                UserId INTEGER REFERENCES Users(Id),
                Date DATE NOT NULL,
                TimeWorked TEXT,
                NoteText TEXT,
                RawText TEXT,
                MoodDetected TEXT,
                IsMilestone INTEGER DEFAULT 0,
                IsArchived INTEGER DEFAULT 0,
                Embedding BLOB,
                SyncedAt DATETIME DEFAULT CURRENT_TIMESTAMP
            );

            -- User preferences
            CREATE TABLE IF NOT EXISTS UserPreferences (
                UserId INTEGER PRIMARY KEY REFERENCES Users(Id),
                ThemeName TEXT DEFAULT 'Office2019Black',
                AccentColor TEXT DEFAULT '#8B0000',
                KegomoDoroWorkMin INTEGER DEFAULT 25,
                KegomoDoroShortBreak INTEGER DEFAULT 5,
                KegomoDoroLongBreak INTEGER DEFAULT 20,
                KegomoDoroBackgroundColor TEXT DEFAULT '#8B0000',
                KegomoDoroMainImagePath TEXT
            );

            -- Create indexes for better performance
            CREATE INDEX IF NOT EXISTS idx_journal_user ON JournalEntries(UserId);
            CREATE INDEX IF NOT EXISTS idx_journal_date ON JournalEntries(Date);


        ";
        
        command.ExecuteNonQuery();
        
        // Migration: Ensure new columns exist for existing databases
        try 
        {
            var migrateCmd = connection.CreateCommand();
            migrateCmd.CommandText = "ALTER TABLE Users ADD COLUMN TotalHours REAL DEFAULT 0;";
            migrateCmd.ExecuteNonQuery();
        } catch { /* Column likely exists */ }
        
        try 
        {
            var migrateCmd = connection.CreateCommand();
            migrateCmd.CommandText = "ALTER TABLE Users ADD COLUMN XP INTEGER DEFAULT 0;";
            migrateCmd.ExecuteNonQuery();
        } catch { /* Column likely exists */ }
        
        try 
        {
            var migrateCmd = connection.CreateCommand();
            migrateCmd.CommandText = "ALTER TABLE Users ADD COLUMN UnlockedAchievements TEXT;";
            migrateCmd.ExecuteNonQuery();
        } catch { /* Column likely exists */ }
        
        try 
        {
            var migrateCmd = connection.CreateCommand();
            migrateCmd.CommandText = "ALTER TABLE Users ADD COLUMN SavedColors TEXT;";
            migrateCmd.ExecuteNonQuery();
        } catch { /* Column likely exists */ }

        _initialized = true;
        _logger.Information("Database initialized successfully");
    }

    public SqliteConnection GetConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }
}
