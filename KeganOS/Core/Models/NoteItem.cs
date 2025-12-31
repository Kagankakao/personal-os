using System;
using System.Collections.Generic;

namespace KeganOS.Core.Models
{
    public class NoteItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty; // Supports Markdown
        public string Category { get; set; } = "General"; // e.g., "Game Ideas", "Life", "Work"
        public List<string> Tags { get; set; } = new List<string>();
        public List<string> ImagePaths { get; set; } = new List<string>();
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime LastModified { get; set; } = DateTime.Now;
        public bool IsPinned { get; set; }
    }
}
