using System;

namespace KeganOS.Core.Models;

/// <summary>
/// Represents a single message in a Prometheus conversation
/// </summary>
public class PrometheusChatMessage
{
    public int Id { get; set; }
    public int ConversationId { get; set; }
    public string Role { get; set; } = "user"; // "user" or "assistant"
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
