using System.Collections.Generic;
using System.Threading.Tasks;
using KeganOS.Core.Models;

namespace KeganOS.Core.Interfaces;

/// <summary>
/// Service for managing Prometheus chat history
/// </summary>
public interface IChatHistoryService
{
    /// <summary>
    /// Get all conversations for a user, ordered by last message time
    /// </summary>
    Task<List<Conversation>> GetConversationsAsync(int? userId);

    /// <summary>
    /// Create a new conversation
    /// </summary>
    Task<Conversation> CreateConversationAsync(int? userId, string? title = null);

    /// <summary>
    /// Get a specific conversation by ID
    /// </summary>
    Task<Conversation?> GetConversationAsync(int conversationId);

    /// <summary>
    /// Get all messages in a conversation
    /// </summary>
    Task<List<PrometheusChatMessage>> GetMessagesAsync(int conversationId);

    /// <summary>
    /// Add a message to a conversation
    /// </summary>
    Task<PrometheusChatMessage> AddMessageAsync(int conversationId, string role, string content);

    /// <summary>
    /// Update conversation title and preview
    /// </summary>
    Task UpdateConversationAsync(int conversationId, string? title = null, string? preview = null);

    /// <summary>
    /// Delete a conversation and all its messages
    /// </summary>
    Task DeleteConversationAsync(int conversationId);
}
