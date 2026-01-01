using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using KeganOS.Core.Interfaces;
using KeganOS.Core.Models;
using KeganOS.Infrastructure.Data;
using Serilog;

namespace KeganOS.Infrastructure.Services;

/// <summary>
/// SQLite-based implementation of chat history service
/// </summary>
public class ChatHistoryService : IChatHistoryService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger _logger = Log.ForContext<ChatHistoryService>();

    public ChatHistoryService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<Conversation>> GetConversationsAsync(int? userId)
    {
        var conversations = new List<Conversation>();
        
        await Task.Run(() =>
        {
            using var connection = _dbContext.GetConnection();
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, UserId, Title, Preview, CreatedAt, LastMessageAt
                FROM Conversations
                WHERE UserId = @userId OR (@userId IS NULL AND UserId IS NULL)
                ORDER BY LastMessageAt DESC
            ";
            cmd.Parameters.AddWithValue("@userId", (object?)userId ?? DBNull.Value);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                conversations.Add(new Conversation
                {
                    Id = reader.GetInt32(0),
                    UserId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                    Title = reader.GetString(2),
                    Preview = reader.IsDBNull(3) ? null : reader.GetString(3),
                    CreatedAt = reader.GetDateTime(4),
                    LastMessageAt = reader.GetDateTime(5)
                });
            }
        });

        return conversations;
    }

    public async Task<Conversation> CreateConversationAsync(int? userId, string? title = null)
    {
        var conversation = new Conversation
        {
            UserId = userId,
            Title = title ?? "New Chat",
            CreatedAt = DateTime.Now,
            LastMessageAt = DateTime.Now
        };

        await Task.Run(() =>
        {
            using var connection = _dbContext.GetConnection();
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Conversations (UserId, Title, CreatedAt, LastMessageAt)
                VALUES (@userId, @title, @createdAt, @lastMessageAt);
                SELECT last_insert_rowid();
            ";
            cmd.Parameters.AddWithValue("@userId", (object?)userId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@title", conversation.Title);
            cmd.Parameters.AddWithValue("@createdAt", conversation.CreatedAt);
            cmd.Parameters.AddWithValue("@lastMessageAt", conversation.LastMessageAt);

            conversation.Id = Convert.ToInt32(cmd.ExecuteScalar());
        });

        _logger.Information("Created conversation {ConversationId} for user {UserId}", conversation.Id, userId);
        return conversation;
    }

    public async Task<Conversation?> GetConversationAsync(int conversationId)
    {
        Conversation? conversation = null;

        await Task.Run(() =>
        {
            using var connection = _dbContext.GetConnection();
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, UserId, Title, Preview, CreatedAt, LastMessageAt
                FROM Conversations
                WHERE Id = @id
            ";
            cmd.Parameters.AddWithValue("@id", conversationId);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                conversation = new Conversation
                {
                    Id = reader.GetInt32(0),
                    UserId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                    Title = reader.GetString(2),
                    Preview = reader.IsDBNull(3) ? null : reader.GetString(3),
                    CreatedAt = reader.GetDateTime(4),
                    LastMessageAt = reader.GetDateTime(5)
                };
            }
        });

        return conversation;
    }

    public async Task<List<PrometheusChatMessage>> GetMessagesAsync(int conversationId)
    {
        var messages = new List<PrometheusChatMessage>();

        await Task.Run(() =>
        {
            using var connection = _dbContext.GetConnection();
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, ConversationId, Role, Content, Timestamp
                FROM ChatMessages
                WHERE ConversationId = @conversationId
                ORDER BY Timestamp ASC
            ";
            cmd.Parameters.AddWithValue("@conversationId", conversationId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                messages.Add(new PrometheusChatMessage
                {
                    Id = reader.GetInt32(0),
                    ConversationId = reader.GetInt32(1),
                    Role = reader.GetString(2),
                    Content = reader.GetString(3),
                    Timestamp = reader.GetDateTime(4)
                });
            }
        });

        return messages;
    }

    public async Task<PrometheusChatMessage> AddMessageAsync(int conversationId, string role, string content)
    {
        var message = new PrometheusChatMessage
        {
            ConversationId = conversationId,
            Role = role,
            Content = content,
            Timestamp = DateTime.Now
        };

        await Task.Run(() =>
        {
            using var connection = _dbContext.GetConnection();
            
            // Insert message
            var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = @"
                INSERT INTO ChatMessages (ConversationId, Role, Content, Timestamp)
                VALUES (@conversationId, @role, @content, @timestamp);
                SELECT last_insert_rowid();
            ";
            insertCmd.Parameters.AddWithValue("@conversationId", conversationId);
            insertCmd.Parameters.AddWithValue("@role", role);
            insertCmd.Parameters.AddWithValue("@content", content);
            insertCmd.Parameters.AddWithValue("@timestamp", message.Timestamp);

            message.Id = Convert.ToInt32(insertCmd.ExecuteScalar());

            // Update conversation's LastMessageAt
            var updateCmd = connection.CreateCommand();
            updateCmd.CommandText = @"
                UPDATE Conversations 
                SET LastMessageAt = @timestamp
                WHERE Id = @conversationId
            ";
            updateCmd.Parameters.AddWithValue("@timestamp", message.Timestamp);
            updateCmd.Parameters.AddWithValue("@conversationId", conversationId);
            updateCmd.ExecuteNonQuery();
        });

        return message;
    }

    public async Task UpdateConversationAsync(int conversationId, string? title = null, string? preview = null)
    {
        await Task.Run(() =>
        {
            using var connection = _dbContext.GetConnection();
            
            var updates = new List<string>();
            var cmd = connection.CreateCommand();

            if (title != null)
            {
                updates.Add("Title = @title");
                cmd.Parameters.AddWithValue("@title", title);
            }
            if (preview != null)
            {
                updates.Add("Preview = @preview");
                cmd.Parameters.AddWithValue("@preview", preview);
            }

            if (updates.Count == 0) return;

            cmd.CommandText = $"UPDATE Conversations SET {string.Join(", ", updates)} WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", conversationId);
            cmd.ExecuteNonQuery();
        });
    }

    public async Task DeleteConversationAsync(int conversationId)
    {
        await Task.Run(() =>
        {
            using var connection = _dbContext.GetConnection();
            var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM Conversations WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", conversationId);
            cmd.ExecuteNonQuery();
        });

        _logger.Information("Deleted conversation {ConversationId}", conversationId);
    }
}
