using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.IO;
using KeganOS.Core.Interfaces;
using KeganOS.Core.Models;
using Serilog;

namespace KeganOS.Infrastructure.Services;

public class PrometheusService : IPrometheusService, IDisposable
{
    private readonly ILogger _logger = Log.ForContext<PrometheusService>();
    private readonly HttpClient _httpClient;
    private readonly IAIProvider _aiProvider;
    private readonly string _prometheusPath;
    private Process? _serverProcess;
    private bool _disposed;
    private string? _geminiApiKey;
    private string? _startedWithApiKey;

    public PrometheusService(IAIProvider aiProvider)
    {
        _aiProvider = aiProvider;
        _httpClient = new HttpClient 
        { 
            BaseAddress = new Uri("http://127.0.0.1:8080"),
            Timeout = TimeSpan.FromMinutes(5) // Increased timeout for long AI responses
        };
        _prometheusPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "prometheus");
        
        // Ensure absolute path
        _prometheusPath = Path.GetFullPath(_prometheusPath);
    }

    /// <summary>
    /// Set the Gemini API key for cloud embeddings. Call this after user login.
    /// </summary>
    public void SetApiKey(string? apiKey)
    {
        _geminiApiKey = apiKey;
    }

    public async Task<string> ConsultAsync(string question, int? userId = null)
    {
        CheckDisposed();
        _logger.Information("Consulting Prometheus for question: {Question}", question);

        try
        {
            // 1. Ensure server is running and using the correct API key
            if (_geminiApiKey != _startedWithApiKey && _serverProcess != null)
            {
                _logger.Information("API Key changed or missing in running server. Restarting Prometheus...");
                await StopServerAsync();
            }

            if (!await IsHealthyAsync())
            {
                if (!await StartServerAsync())
                    return $"Prometheus server is not running and failed to start.\nError: {GetLastError()}";
            }

            // 2. Get semantic context + memory from RAG server
            var request = new { question = question, limit = 10, user_id = userId };
            var response = await _httpClient.PostAsJsonAsync("/ask", request);
            
            if (!response.IsSuccessStatusCode)
                return $"Prometheus search failed: {response.ReasonPhrase}";

            var jsonResult = await response.Content.ReadFromJsonAsync<JsonElement>();
            var contextItems = jsonResult.GetProperty("context");
            
            // Parse memory if available
            var memoryContext = "";
            if (jsonResult.TryGetProperty("memory", out var memoryItems) && memoryItems.GetArrayLength() > 0)
            {
                memoryContext = "\n\nYou also remember these past conversations:\n";
                foreach (var mem in memoryItems.EnumerateArray())
                {
                    var prevQuestion = mem.TryGetProperty("question", out var q) ? q.GetString() : "";
                    var prevText = mem.GetProperty("text").GetString();
                    memoryContext += $"- When asked '{prevQuestion}', {prevText}\n";
                }
            }

            // Handle empty journal
            if (contextItems.GetArrayLength() == 0 && string.IsNullOrEmpty(memoryContext))
            {
                var helpPrompt = "You are Prometheus, a warm AI companion. The user asked: \"" + question + "\". " +
                    "Their journal is empty and you have no memories yet. Welcome them warmly and encourage them to start journaling. Be brief and friendly.";
                var helpResponse = await _aiProvider.GenerateResponseAsync(helpPrompt);
                await RememberConversation(question, helpResponse, userId);
                return helpResponse;
            }

            // 3. Build prompt with journal context, memory, and TIME AWARENESS
            var currentDate = DateTime.Now.ToString("MMMM d, yyyy");
            var currentTime = DateTime.Now.ToString("h:mm tt");
            
            var contextText = "";
            if (contextItems.GetArrayLength() > 0)
            {
                contextText = "From your journal:\n";
                foreach (var item in contextItems.EnumerateArray())
                {
                    var date = item.GetProperty("date").GetString();
                    var text = item.GetProperty("text").GetString();
                    contextText += $"- [{date}] {text}\n";
                }
            }

            var fullPrompt = $"You are Prometheus, a warm and supportive AI companion who knows the user personally. " +
                             $"You have genuine memory of past conversations and can reference them naturally. " +
                             $"Speak casually like a close friend. Be encouraging, insightful, and occasionally playful.\n\n" +
                             $"IMPORTANT - Today is {currentDate}, current time is {currentTime}. Use this to correctly reason about dates.\n" +
                             $"If you recall saying something wrong in a past conversation (like a wrong date), acknowledge it gracefully: " +
                             $"\"Oh wait, I think I got that wrong before...\" - this makes you feel more human, not less.\n\n" +
                             $"{contextText}{memoryContext}\n\n" +
                             $"User's question: {question}\n\n" +
                             $"Respond conversationally. Reference past conversations naturally if relevant:";

            // 4. Generate AI response
            var aiResponse = await _aiProvider.GenerateResponseAsync(fullPrompt);
            
            // 5. Remember this conversation for the future
            await RememberConversation(question, aiResponse, userId);
            
            return aiResponse;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Consultation failed");
            return $"Error consulting Prometheus: {ex.Message}";
        }
    }

    private async Task RememberConversation(string question, string response, int? userId)
    {
        try
        {
            var rememberRequest = new { question = question, response = response, user_id = userId };
            await _httpClient.PostAsJsonAsync("/remember", rememberRequest);
            _logger.Debug("Conversation saved to Prometheus memory");
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to save conversation to memory");
        }
    }

    public async IAsyncEnumerable<string> ConsultStreamingAsync(string question, int? userId = null, IEnumerable<(string Role, string Message)>? conversationHistory = null)
    {
        CheckDisposed();
        _logger.Information("Consulting Prometheus (streaming) for question: {Question}", question);

        // Build prompt with context - move to separate async method to avoid yield in try-catch
        var (fullPrompt, error) = await BuildStreamingPromptAsync(question, userId, conversationHistory);
        
        if (error != null)
        {
            yield return error;
            yield break;
        }

        // Stream the response (outside try-catch)
        var fullResponse = new System.Text.StringBuilder();
        await foreach (var chunk in _aiProvider.GenerateResponseStreamingAsync(fullPrompt!))
        {
            fullResponse.Append(chunk);
            yield return chunk;
        }

        // Remember conversation after streaming is complete
        _ = RememberConversation(question, fullResponse.ToString(), userId);
    }

    private async Task<(string? Prompt, string? Error)> BuildStreamingPromptAsync(string question, int? userId, IEnumerable<(string Role, string Message)>? conversationHistory = null)
    {
        try
        {
            if (_geminiApiKey != _startedWithApiKey && _serverProcess != null)
            {
                _logger.Information("API Key changed or missing in running server. Restarting Prometheus...");
                await StopServerAsync();
            }

            if (!await IsHealthyAsync())
            {
                if (!await StartServerAsync())
                {
                    return (null, $"Prometheus server failed to start: {GetLastError()}");
                }
            }

            var request = new { question = question, limit = 10, user_id = userId };
            var response = await _httpClient.PostAsJsonAsync("/ask", request);
            
            if (!response.IsSuccessStatusCode)
            {
                return (null, $"Prometheus search failed: {response.ReasonPhrase}");
            }

            var jsonResult = await response.Content.ReadFromJsonAsync<JsonElement>();
            var contextItems = jsonResult.GetProperty("context");
            
            var memoryContext = "";
            if (jsonResult.TryGetProperty("memory", out var memoryItems) && memoryItems.GetArrayLength() > 0)
            {
                memoryContext = "\n\nYou also remember these past conversations:\n";
                foreach (var mem in memoryItems.EnumerateArray())
                {
                    var prevQuestion = mem.TryGetProperty("question", out var q) ? q.GetString() : "";
                    var prevText = mem.GetProperty("text").GetString();
                    memoryContext += $"- When asked '{prevQuestion}', {prevText}\n";
                }
            }

            var currentDate = DateTime.Now.ToString("MMMM d, yyyy");
            var currentTime = DateTime.Now.ToString("h:mm tt");
            
            var contextText = "";
            if (contextItems.GetArrayLength() > 0)
            {
                contextText = "From your journal:\n";
                foreach (var item in contextItems.EnumerateArray())
                {
                    var date = item.GetProperty("date").GetString();
                    var text = item.GetProperty("text").GetString();
                    contextText += $"- [{date}] {text}\n";
                }
            }
            
            // Build conversation history context
            var conversationContext = "";
            if (conversationHistory != null && conversationHistory.Any())
            {
                conversationContext = "\n\nRecent conversation:\n";
                foreach (var (role, msg) in conversationHistory)
                {
                    var label = role == "user" ? "User" : "You";
                    conversationContext += $"{label}: {msg}\n";
                }
            }

            var fullPrompt = $"You are Prometheus, a warm and supportive AI companion who knows the user personally. " +
                             $"Speak casually like a close friend. Be encouraging and occasionally playful.\n\n" +
                             $"IMPORTANT RULES:\n" +
                             $"- Use ASCII emoticons like :D, :), :(, :P, ;), >:C, :-O, <3 instead of Unicode emojis\n" +
                             $"- Never use emoji characters like 👋 🔥 😊 etc.\n\n" +
                             $"Today is {currentDate}, current time is {currentTime}.\n\n" +
                             $"{contextText}{memoryContext}{conversationContext}\n\n" +
                             $"User's question: {question}\n\n" +
                             $"Respond conversationally:";
            
            return (fullPrompt, null);
        }
        catch (Exception ex)
        {
            return (null, $"Error: {ex.Message}");
        }
    }

    public async Task<bool> SyncDatabaseAsync()
    {
        CheckDisposed();
        try
        {
            if (!await IsHealthyAsync()) await StartServerAsync();
            
            var response = await _httpClient.PostAsJsonAsync("/sync", new { });
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/health");
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    private string _lastError = "No errors recorded.";

    public string GetLastError() => _lastError;

    public async Task<bool> StartServerAsync()
    {
        if (await IsHealthyAsync()) return true;

        _logger.Information("Starting Prometheus Python server at {Path}", _prometheusPath);

        try
        {
            var venvPython = Path.Combine(_prometheusPath, "venv", "bin", "python.exe");
            if (!File.Exists(venvPython)) 
                venvPython = Path.Combine(_prometheusPath, "venv", "Scripts", "python.exe");

            var startInfo = new ProcessStartInfo
            {
                FileName = venvPython,
                Arguments = "-m prometheus.server",
                WorkingDirectory = Path.Combine(_prometheusPath, ".."),
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            // Set DB path env var
            startInfo.EnvironmentVariables["KEGANOS_DB_PATH"] = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "keganos.db"));
            
            // Critical: Add MSYS2 bin to PATH so it can find its own DLLs when run from C#
            var msysBin = @"C:\msys64\ucrt64\bin";
            var currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
            if (!currentPath.Contains(msysBin))
            {
                startInfo.EnvironmentVariables["PATH"] = $"{msysBin};{currentPath}";
            }

            // Critical: Add MSYS2 site-packages to PYTHONPATH
            var msysSitePackages = @"C:\msys64\ucrt64\lib\python3.12\site-packages";
            var currentPythonPath = Environment.GetEnvironmentVariable("PYTHONPATH") ?? "";
            startInfo.EnvironmentVariables["PYTHONPATH"] = string.IsNullOrEmpty(currentPythonPath) 
                ? msysSitePackages 
                : $"{msysSitePackages};{currentPythonPath}";

            // Pass Gemini API key for cloud embeddings
            if (!string.IsNullOrEmpty(_geminiApiKey))
            {
                startInfo.EnvironmentVariables["GOOGLE_API_KEY"] = _geminiApiKey;
                _startedWithApiKey = _geminiApiKey;
            }
            else
            {
                _startedWithApiKey = null;
            }

            _serverProcess = Process.Start(startInfo);
            
            // Wait for it to become healthy
            for (int i = 0; i < 15; i++)
            {
                if (await IsHealthyAsync()) 
                {
                    _logger.Information("Prometheus server started and healthy.");
                    _lastError = "Healthy";
                    return true;
                }
                
                // If it exited early, something is wrong
                if (_serverProcess != null && _serverProcess.HasExited)
                {
                    _lastError = await _serverProcess.StandardError.ReadToEndAsync();
                    _logger.Error("Prometheus server exited early with error: {Error}", _lastError);
                    break;
                }

                await Task.Delay(1000);
            }

            if (_lastError == "Healthy") _lastError = "Server started but timed out becoming healthy.";
            _logger.Warning("Prometheus server failed to become healthy within timeout.");
            return false;
        }
        catch (Exception ex)
        {
            _lastError = $"Exception starting server: {ex.Message}";
            _logger.Error(ex, "Failed to start Prometheus server");
            return false;
        }
    }

    public Task StopServerAsync()
    {
        if (_serverProcess != null && !_serverProcess.HasExited)
        {
            _serverProcess.Kill();
            _serverProcess = null;
        }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopServerAsync().Wait();
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }

    private void CheckDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(PrometheusService));
    }
}
