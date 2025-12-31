using System;

namespace KeganOS.Core.Models
{
    public class TaskItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Text { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
        public string Category { get; set; } = "Daily"; // Daily, LongTerm, Done
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? CompletedAt { get; set; }
    }
}
