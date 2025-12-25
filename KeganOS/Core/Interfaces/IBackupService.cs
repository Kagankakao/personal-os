using KeganOS.Core.Models;

namespace KeganOS.Core.Interfaces;

/// <summary>
/// Service for backup and restore operations
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// Create a full backup for a user
    /// </summary>
    Task<BackupResult> CreateBackupAsync(User user);

    /// <summary>
    /// Backup the journal file before modifications
    /// </summary>
    Task<string?> BackupJournalAsync(User user);

    /// <summary>
    /// Backup an image file (e.g. avatar) before replacement
    /// </summary>
    Task<string?> BackupImageAsync(User user, string imagePath);

    /// <summary>
    /// List available backups for a user
    /// </summary>
    Task<IEnumerable<BackupInfo>> GetBackupsAsync(User user);

    /// <summary>
    /// Restore from a specific backup
    /// </summary>
    Task<bool> RestoreBackupAsync(User user, DateTime backupDate);

    /// <summary>
    /// Clean up old backups, keeping the most recent ones
    /// </summary>
    Task CleanupOldBackupsAsync(User user, int keepCount = 10);

    /// <summary>
    /// Get the backup directory path for a user
    /// </summary>
    string GetBackupPath(User user);
}

/// <summary>
/// Result of a backup operation
/// </summary>
public record BackupResult(bool Success, string? Path, string? Error);

/// <summary>
/// Information about an existing backup
/// </summary>
public record BackupInfo(DateTime Date, string Path, long SizeBytes, string Type);
