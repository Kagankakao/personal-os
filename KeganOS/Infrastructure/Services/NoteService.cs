using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using KeganOS.Core.Interfaces;
using KeganOS.Core.Models;
using KeganOS.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Serilog;

namespace KeganOS.Infrastructure.Services
{
    public class NoteService : INoteService
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger _logger = Log.ForContext<NoteService>();

        public NoteService(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<NoteItem>> GetNotesAsync(int userId)
        {
            var notes = new List<NoteItem>();
            try
            {
                using var connection = _dbContext.GetConnection();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT * FROM Notes WHERE UserId = @userId AND IsDeleted = 0 ORDER BY IsPinned DESC, LastModified DESC";
                command.Parameters.AddWithValue("@userId", userId);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    notes.Add(MapReaderToNote(reader));
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting notes for user {UserId}", userId);
            }
            return notes;
        }

        public async Task<NoteItem> GetNoteByIdAsync(string noteId)
        {
            try
            {
                using var connection = _dbContext.GetConnection();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT * FROM Notes WHERE Id = @id";
                command.Parameters.AddWithValue("@id", noteId);

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return MapReaderToNote(reader);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting note {NoteId}", noteId);
            }
            return null;
        }

        public async Task SaveNoteAsync(int userId, NoteItem note)
        {
            try
            {
                using var connection = _dbContext.GetConnection();
                
                // BACKUP LOGIC: Copy current state to NoteHistory if it exists
                var checkCmd = connection.CreateCommand();
                checkCmd.CommandText = "SELECT * FROM Notes WHERE Id = @id";
                checkCmd.Parameters.AddWithValue("@id", note.Id);
                
                using (var reader = await checkCmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        var backupCmd = connection.CreateCommand();
                        backupCmd.CommandText = @"
                            INSERT INTO NoteHistory (NoteId, Title, Content, Category, Tags, ImagePaths)
                            VALUES (@id, @title, @content, @category, @tags, @images)";
                        backupCmd.Parameters.AddWithValue("@id", reader["Id"]);
                        backupCmd.Parameters.AddWithValue("@title", reader["Title"]);
                        backupCmd.Parameters.AddWithValue("@content", reader["Content"]);
                        backupCmd.Parameters.AddWithValue("@category", reader["Category"]);
                        backupCmd.Parameters.AddWithValue("@tags", reader["Tags"]);
                        backupCmd.Parameters.AddWithValue("@images", reader["ImagePaths"]);
                        await backupCmd.ExecuteNonQueryAsync();
                    }
                }

                // UPSERT LOGIC
                var upsertCmd = connection.CreateCommand();
                upsertCmd.CommandText = @"
                    INSERT INTO Notes (Id, UserId, Title, Content, Category, Tags, ImagePaths, IsPinned, LastModified)
                    VALUES (@id, @userId, @title, @content, @category, @tags, @images, @pinned, @modified)
                    ON CONFLICT(Id) DO UPDATE SET
                        Title = excluded.Title,
                        Content = excluded.Content,
                        Category = excluded.Category,
                        Tags = excluded.Tags,
                        ImagePaths = excluded.ImagePaths,
                        IsPinned = excluded.IsPinned,
                        LastModified = excluded.LastModified";

                upsertCmd.Parameters.AddWithValue("@id", note.Id);
                upsertCmd.Parameters.AddWithValue("@userId", userId);
                upsertCmd.Parameters.AddWithValue("@title", note.Title);
                upsertCmd.Parameters.AddWithValue("@content", note.Content);
                upsertCmd.Parameters.AddWithValue("@category", note.Category);
                upsertCmd.Parameters.AddWithValue("@tags", JsonSerializer.Serialize(note.Tags));
                upsertCmd.Parameters.AddWithValue("@images", JsonSerializer.Serialize(note.ImagePaths));
                upsertCmd.Parameters.AddWithValue("@pinned", note.IsPinned ? 1 : 0);
                upsertCmd.Parameters.AddWithValue("@modified", DateTime.Now);

                await upsertCmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error saving note {NoteId}", note.Id);
            }
        }

        public async Task DeleteNoteAsync(string noteId)
        {
            try
            {
                using var connection = _dbContext.GetConnection();
                var command = connection.CreateCommand();
                command.CommandText = "UPDATE Notes SET IsDeleted = 1 WHERE Id = @id";
                command.Parameters.AddWithValue("@id", noteId);
                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error soft-deleting note {NoteId}", noteId);
            }
        }

        public async Task<List<NoteItem>> SearchNotesAsync(int userId, string query)
        {
            var notes = new List<NoteItem>();
            try
            {
                using var connection = _dbContext.GetConnection();
                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT * FROM Notes 
                    WHERE UserId = @userId AND IsDeleted = 0 
                    AND (Title LIKE @query OR Content LIKE @query OR Category LIKE @query OR Tags LIKE @query)
                    ORDER BY IsPinned DESC, LastModified DESC";
                command.Parameters.AddWithValue("@userId", userId);
                command.Parameters.AddWithValue("@query", $"%{query}%");

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    notes.Add(MapReaderToNote(reader));
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error searching notes for user {UserId}", userId);
            }
            return notes;
        }

        public async Task<List<NoteItem>> GetPinnedNotesAsync(int userId)
        {
            var notes = new List<NoteItem>();
            try
            {
                using var connection = _dbContext.GetConnection();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT * FROM Notes WHERE UserId = @userId AND IsPinned = 1 AND IsDeleted = 0 ORDER BY LastModified DESC";
                command.Parameters.AddWithValue("@userId", userId);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    notes.Add(MapReaderToNote(reader));
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting pinned notes for user {UserId}", userId);
            }
            return notes;
        }

        private NoteItem MapReaderToNote(SqliteDataReader reader)
        {
            return new NoteItem
            {
                Id = reader["Id"].ToString(),
                Title = reader["Title"]?.ToString() ?? "",
                Content = reader["Content"]?.ToString() ?? "",
                Category = reader["Category"]?.ToString() ?? "General",
                Tags = JsonSerializer.Deserialize<List<string>>(reader["Tags"]?.ToString() ?? "[]") ?? new List<string>(),
                ImagePaths = JsonSerializer.Deserialize<List<string>>(reader["ImagePaths"]?.ToString() ?? "[]") ?? new List<string>(),
                CreatedAt = Convert.ToDateTime(reader["CreatedAt"]),
                LastModified = Convert.ToDateTime(reader["LastModified"]),
                IsPinned = Convert.ToInt32(reader["IsPinned"]) == 1
            };
        }
    }
}
