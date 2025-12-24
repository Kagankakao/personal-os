using KeganOS.Core.Interfaces;
using Mscc.GenerativeAI;
using Serilog;
using AppChatMessage = KeganOS.Core.Interfaces.ChatMessage;

namespace KeganOS.Infrastructure.Services;

public class GeminiProvider : IAIProvider, IDisposable
{
    private readonly ILogger _logger = Log.ForContext<GeminiProvider>();
    private GoogleAI? _googleAI;
    private GenerativeModel? _model;
    private string? _apiKey;
    private bool _disposed;

    public bool IsAvailable => !string.IsNullOrEmpty(_apiKey) && _model != null;

    public void Configure(string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey)) return;
        try
        {
            _apiKey = apiKey;
            _googleAI = new GoogleAI(apiKey);
            _model = _googleAI.GenerativeModel(Model.Gemini15Flash);
            _logger.Information("Gemini AI configured");
        }
        catch (Exception ex) { _logger.Error(ex, "Failed to configure Gemini"); }
    }

    public async Task<string> GenerateResponseAsync(string prompt, IEnumerable<AppChatMessage>? history = null)
    {
        if (!IsAvailable) return "AI not configured. Add API key in settings.";
        try
        {
            var response = await _model!.GenerateContent(prompt);
            return response.Text ?? "No response";
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    public Task<float[]> GenerateEmbeddingAsync(string text) => Task.FromResult(Array.Empty<float>());

    public async Task<string> InterpretQuoteAsync(string quote, string context)
    {
        if (!IsAvailable) return quote;
        try
        {
            var response = await _model!.GenerateContent($"Interpret: {quote}");
            return response.Text ?? quote;
        }
        catch { return quote; }
    }

    public void Dispose() { _disposed = true; GC.SuppressFinalize(this); }
}