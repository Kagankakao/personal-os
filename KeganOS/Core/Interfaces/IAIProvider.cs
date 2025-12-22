using KeganOS.Core.Models;

namespace KeganOS.Core.Interfaces;

/// <summary>
/// Abstraction for AI providers (SOLID - Dependency Inversion)
/// Can be implemented by GeminiProvider, OpenAIProvider, OllamaProvider, etc.
/// </summary>
public interface IAIProvider
{
    /// <summary>
    /// Check if AI is available (internet connection, API key valid)
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Generate a response from the AI
    /// </summary>
    Task<string> GenerateResponseAsync(string prompt, IEnumerable<ChatMessage>? history = null);

    /// <summary>
    /// Generate embeddings for RAG
    /// </summary>
    Task<float[]> GenerateEmbeddingAsync(string text);

    /// <summary>
    /// Interpret a quote from user's journal with AI insight
    /// </summary>
    Task<string> InterpretQuoteAsync(string quote, string context);
}

/// <summary>
/// Chat message for conversation history
/// </summary>
public record ChatMessage(string Role, string Content);
