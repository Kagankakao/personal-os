using System;
using System.IO;
using System.Text.Json;
using KeganOS.Core.Interfaces;
using KeganOS.Core.Models;
using Serilog;

namespace KeganOS.Infrastructure.Services;

/// <summary>
/// Service for backup and restore operations
/// </summary>
public class BackupService : IBackupService
{
    private readonly ILogger _logger = Log.ForContext<BackupService>();
    private readonly string _baseBackupPath;

    public BackupService()
    {
        _baseBackupPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KeganOS", "backups");
    }

    public string GetBackupPath(User user)
    {
        var safeUserName = string.Join("_", user.DisplayName.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_baseBackupPath, safeUserName);
    }

    public async Task<BackupResult> CreateBackupAsync(User user)
    {
        try
        {
            var backupDir = GetBackupPath(user);
            Directory.CreateDirectory(backupDir);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            
            // Find database - check multiple possible locations
            var possibleDbPaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "keganos.db"),
                Path.Combine(Environment.CurrentDirectory, "keganos.db"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KeganOS", "keganos.db"),
                "keganos.db"
            };
            
            var dbPath = possibleDbPaths.FirstOrDefault(File.Exists);

            if (!string.IsNullOrEmpty(dbPath) && File.Exists(dbPath))
            {
                var dbBackupPath = Path.Combine(backupDir, $"db_{timestamp}.db");
                File.Copy(dbPath, dbBackupPath, overwrite: true);
                _logger.Information("Database backed up from {Source} to {Path}", dbPath, dbBackupPath);
            }
            else
            {
                _logger.Warning("No database found to backup. Searched paths: {Paths}", string.Join(", ", possibleDbPaths));
            }

            // Backup journal
            var journalBackupPath = await BackupJournalAsync(user);
            
            // Backup avatar image if it exists
            string? avatarBackupPath = null;
            if (!string.IsNullOrEmpty(user.AvatarPath) && File.Exists(user.AvatarPath))
            {
                avatarBackupPath = await BackupImageAsync(user, user.AvatarPath);
            }
            
            // Create manifest with all backup info
            var manifest = new
            {
                UserId = user.Id,
                UserName = user.DisplayName,
                BackupDate = DateTime.Now,
                HasDatabase = !string.IsNullOrEmpty(dbPath),
                HasJournal = !string.IsNullOrEmpty(journalBackupPath),
                HasAvatar = !string.IsNullOrEmpty(avatarBackupPath),
                AvatarOriginalPath = user.AvatarPath
            };

            var manifestPath = Path.Combine(backupDir, $"manifest_{timestamp}.json");
            var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(manifestPath, json);

            _logger.Information("Full backup created for {User} at {Path} (DB: {DB}, Journal: {J}, Avatar: {A})", 
                user.DisplayName, backupDir, 
                !string.IsNullOrEmpty(dbPath), 
                !string.IsNullOrEmpty(journalBackupPath),
                !string.IsNullOrEmpty(avatarBackupPath));
            
            // Cleanup old backups
            await CleanupOldBackupsAsync(user);

            return new BackupResult(true, backupDir, null);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to create backup for {User}", user.DisplayName);
            return new BackupResult(false, null, ex.Message);
        }
    }

    public async Task<string?> BackupJournalAsync(User user)
    {
        try
        {
            var kegomoDoroPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "KEGOMODORO");

            var journalPath = Path.Combine(kegomoDoroPath, user.JournalFileName ?? "diary.txt");

            if (!File.Exists(journalPath))
            {
                _logger.Debug("No journal file to backup: {Path}", journalPath);
                return null;
            }

            var backupDir = Path.Combine(GetBackupPath(user), "journals");
            Directory.CreateDirectory(backupDir);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupPath = Path.Combine(backupDir, $"journal_{timestamp}.txt");

            File.Copy(journalPath, backupPath, overwrite: true);
            _logger.Information("Journal backed up: {Path}", backupPath);

            return backupPath;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to backup journal for {User}", user.DisplayName);
            return null;
        }
    }

    public async Task<string?> BackupImageAsync(User user, string imagePath)
    {
        try
        {
            if (!File.Exists(imagePath))
            {
                _logger.Warning("Image file not found for backup: {Path}", imagePath);
                return null;
            }

            var backupDir = Path.Combine(GetBackupPath(user), "images");
            Directory.CreateDirectory(backupDir);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var ext = Path.GetExtension(imagePath);
            var name = Path.GetFileNameWithoutExtension(imagePath);
            var backupPath = Path.Combine(backupDir, $"{name}_{timestamp}{ext}");

            File.Copy(imagePath, backupPath, overwrite: true);
            _logger.Information("Image backed up: {Path}", backupPath);
            
            // Auto cleanup old images (keep last 5)
            await CleanupOldImagesAsync(user, 5);

            return backupPath;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to backup image: {Path}", imagePath);
            return null;
        }
    }

    private async Task CleanupOldImagesAsync(User user, int keepCount)
    {
        try
        {
            var imagesDir = Path.Combine(GetBackupPath(user), "images");
            if (!Directory.Exists(imagesDir)) return;

            var imageFiles = Directory.GetFiles(imagesDir)
                .OrderByDescending(f => f)
                .Skip(keepCount)
                .ToList();

            foreach (var oldImage in imageFiles)
            {
                File.Delete(oldImage);
                _logger.Debug("Deleted old image backup: {Path}", oldImage);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to cleanup old image backups");
        }
    }

    public async Task<IEnumerable<BackupInfo>> GetBackupsAsync(User user)
    {
        var backups = new List<BackupInfo>();

        try
        {
            var backupDir = GetBackupPath(user);
            if (!Directory.Exists(backupDir))
                return backups;

            // Find manifest files
            var manifestFiles = Directory.GetFiles(backupDir, "manifest_*.json");
            
            foreach (var manifestPath in manifestFiles)
            {
                try
                {
                    var fileInfo = new FileInfo(manifestPath);
                    var json = await File.ReadAllTextAsync(manifestPath);
                    var manifest = JsonSerializer.Deserialize<JsonElement>(json);
                    
                    if (manifest.TryGetProperty("BackupDate", out var dateElement))
                    {
                        var date = dateElement.GetDateTime();
                        backups.Add(new BackupInfo(date, backupDir, fileInfo.Length, "full"));
                    }
                }
                catch { /* Skip invalid manifests */ }
            }

            // Also check for journal-only backups
            var journalDir = Path.Combine(backupDir, "journals");
            if (Directory.Exists(journalDir))
            {
                foreach (var journalBackup in Directory.GetFiles(journalDir, "journal_*.txt"))
                {
                    var fileInfo = new FileInfo(journalBackup);
                    // Extract date from filename
                    var fileName = Path.GetFileNameWithoutExtension(journalBackup);
                    if (fileName.StartsWith("journal_") && fileName.Length >= 16)
                    {
                        var datePart = fileName.Substring(8, 8);
                        if (DateTime.TryParseExact(datePart, "yyyyMMdd", null, 
                            System.Globalization.DateTimeStyles.None, out var date))
                        {
                            backups.Add(new BackupInfo(date, journalBackup, fileInfo.Length, "journal"));
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get backups for {User}", user.DisplayName);
        }

        return backups.OrderByDescending(b => b.Date);
    }

    public async Task<bool> RestoreBackupAsync(User user, DateTime backupDate)
    {
        try
        {
            var backupDir = GetBackupPath(user);
            var dateStr = backupDate.ToString("yyyyMMdd");

            // Find the backup from that date
            var journalDir = Path.Combine(backupDir, "journals");
            if (Directory.Exists(journalDir))
            {
                var journalBackups = Directory.GetFiles(journalDir, $"journal_{dateStr}*.txt");
                if (journalBackups.Length > 0)
                {
                    var journalBackup = journalBackups.OrderByDescending(f => f).First();
                    var kegomoDoroPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "KEGOMODORO");
                    var journalPath = Path.Combine(kegomoDoroPath, user.JournalFileName ?? "diary.txt");

                    // Backup current before restore
                    await BackupJournalAsync(user);

                    // Restore
                    File.Copy(journalBackup, journalPath, overwrite: true);
                    _logger.Information("Restored journal from {Date} for {User}", backupDate, user.DisplayName);
                    return true;
                }
            }

            _logger.Warning("No backup found for {Date}", backupDate);
            return false;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to restore backup for {User}", user.DisplayName);
            return false;
        }
    }

    public async Task CleanupOldBackupsAsync(User user, int keepCount = 10)
    {
        try
        {
            var journalDir = Path.Combine(GetBackupPath(user), "journals");
            if (!Directory.Exists(journalDir))
                return;

            var backupFiles = Directory.GetFiles(journalDir, "journal_*.txt")
                .OrderByDescending(f => f)
                .Skip(keepCount)
                .ToList();

            foreach (var oldBackup in backupFiles)
            {
                File.Delete(oldBackup);
                _logger.Debug("Deleted old backup: {Path}", oldBackup);
            }

            if (backupFiles.Count > 0)
            {
                _logger.Information("Cleaned up {Count} old journal backups", backupFiles.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to cleanup old backups");
        }
    }
}
