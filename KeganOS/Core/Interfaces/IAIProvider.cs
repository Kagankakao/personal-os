using KeganOS.Core.Models;

namespace KeganOS.Core.Interfaces;

public interface IAIProvider
{
    bool IsAvailable { get; }
    void Configure(string apiKey);
    Task<string> GenerateResponseAsync(string prompt, IEnumerable<ChatMessage>? history = null);
    Task<float[]> GenerateEmbeddingAsync(string text);
    Task<string> InterpretQuoteAsync(string quote, string context);
}

public record ChatMessage(string Role, string Content);