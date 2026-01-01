using System;

namespace KeganOS.Core.Models;

/// <summary>
/// Represents a chat conversation session with Prometheus
/// </summary>
public class Conversation
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string Title { get; set; } = "New Chat";
    public string? Preview { get; set; } // First line of last AI response
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime LastMessageAt { get; set; } = DateTime.Now;
}
