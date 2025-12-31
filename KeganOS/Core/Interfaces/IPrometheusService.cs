using System.Collections.Generic;
using System.Threading.Tasks;

namespace KeganOS.Core.Interfaces;

/// <summary>
/// Interface for Prometheus AI RAG system
/// </summary>
public interface IPrometheusService
{
    /// <summary>
    /// Consult Prometheus about the user's journey using semantic search
    /// </summary>
    /// <param name="question">The user's question or prompt</param>
    /// <param name="userId">The current user ID</param>
    /// <returns>A comprehensive response from Prometheus</returns>
    Task<string> ConsultAsync(string question, int? userId = null);

    /// <summary>
    /// Consult Prometheus with streaming response for real-time display
    /// </summary>
    IAsyncEnumerable<string> ConsultStreamingAsync(string question, int? userId = null, IEnumerable<(string Role, string Message)>? conversationHistory = null);

    /// <summary>
    /// Trigger a synchronization between the primary database and the semantic index
    /// </summary>
    /// <returns>True if sync was successful</returns>
    Task<bool> SyncDatabaseAsync();

    /// <summary>
    /// Check if the Prometheus local server is running and healthy
    /// </summary>
    Task<bool> IsHealthyAsync();

    /// <summary>
    /// Start the Prometheus local server process
    /// </summary>
    Task<bool> StartServerAsync();

    Task StopServerAsync();

    /// <summary>
    /// Get the last error message from the server process
    /// </summary>
    string GetLastError();
}

public class PrometheusResult
{
    public string Text { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public float Score { get; set; }
}
