using KeganOS.Core.Interfaces;
using KeganOS.Core.Models;
using Serilog;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KeganOS.Infrastructure.Services;

/// <summary>
/// Service for Pixe.la habit tracking API integration
/// </summary>
public class PixelaService : IPixelaService, IDisposable
{
    private readonly ILogger _logger = Log.ForContext<PixelaService>();
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://pixe.la/v1/users";
    private bool _disposed;

    public PixelaService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _logger.Debug("PixelaService initialized");
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient.Dispose();
            _disposed = true;
            _logger.Debug("PixelaService disposed");
        }
        GC.SuppressFinalize(this);
    }

    public bool IsConfigured(User user) =>
        !string.IsNullOrEmpty(user.PixelaUsername) &&
        !string.IsNullOrEmpty(user.PixelaToken) &&
        !string.IsNullOrEmpty(user.PixelaGraphId);

    public async Task<PixelaStats?> GetStatsAsync(User user)
    {
        if (!IsConfigured(user))
        {
            _logger.Warning("Pixe.la not configured for user {User}", user.DisplayName);
            return null;
        }

        _logger.Information("Fetching Pixe.la stats for {User}/{Graph}", 
            user.PixelaUsername, user.PixelaGraphId);

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"{BaseUrl}/{user.PixelaUsername}/graphs/{user.PixelaGraphId}/stats");
            request.Headers.Add("X-USER-TOKEN", user.PixelaToken);

            var response = await _httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.Warning("Pixe.la API returned {Status}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<PixelaStatsResponse>(json);

            if (data == null) return null;

            var stats = new PixelaStats
            {
                TotalPixelsCount = data.TotalPixelsCount,
                TotalQuantity = data.TotalQuantity,
                MaxQuantity = data.MaxQuantity,
                MinQuantity = data.MinQuantity,
                AvgQuantity = data.AvgQuantity
            };

            _logger.Information("Stats retrieved: Total={Total}h, Max={Max}h, Avg={Avg}h",
                stats.TotalQuantity, stats.MaxQuantity, stats.AvgQuantity);

            return stats;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to fetch Pixe.la stats");
            return null;
        }
    }

    public async Task<IEnumerable<PixelaPixel>> GetPixelsAsync(User user, DateTime? from = null, DateTime? to = null)
    {
        if (!IsConfigured(user))
        {
            _logger.Warning("Pixe.la not configured for user {User}", user.DisplayName);
            return [];
        }

        _logger.Information("Fetching Pixe.la pixels for {User}/{Graph}", 
            user.PixelaUsername, user.PixelaGraphId);

        try
        {
            var url = $"{BaseUrl}/{user.PixelaUsername}/graphs/{user.PixelaGraphId}/pixels?withBody=true";
            
            if (from.HasValue)
                url += $"&from={from.Value:yyyyMMdd}";
            if (to.HasValue)
                url += $"&to={to.Value:yyyyMMdd}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("X-USER-TOKEN", user.PixelaToken);

            var response = await _httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.Warning("Pixe.la API returned {Status}", response.StatusCode);
                return [];
            }

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<PixelaPixelsResponse>(json);

            if (data?.Pixels == null) return [];

            var pixels = data.Pixels.Select(p => new PixelaPixel
            {
                Date = DateTime.ParseExact(p.Date, "yyyyMMdd", null),
                Quantity = double.TryParse(p.Quantity, out var q) ? q : 0
            }).ToList();

            _logger.Information("Retrieved {Count} pixels", pixels.Count);
            return pixels;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to fetch Pixe.la pixels");
            return [];
        }
    }

    /// <summary>
    /// Generate a token from username (like KEGOMODORO does)
    /// </summary>
    public string GenerateToken(string username)
    {
        // Similar to KEGOMODORO's approach - deterministic token from username
        return $"{username}token{username.Length}secret";
    }

    public async Task<(bool isAvailable, string? error)> CheckUsernameAvailabilityAsync(string username)
    {
        _logger.Information("Checking Pixe.la username: {Username}", username);

        if (string.IsNullOrWhiteSpace(username))
            return (false, "Username is required");

        if (username.Length < 1 || username.Length > 32)
            return (false, "Username must be 1-32 characters");

        // Pixe.la usernames must be lowercase letters, numbers, and hyphens
        if (!System.Text.RegularExpressions.Regex.IsMatch(username, @"^[a-z][a-z0-9-]*$"))
            return (false, "Username must start with a letter and contain only lowercase letters, numbers, and hyphens");

        // Username format is valid
        return (true, null);
    }

    /// <summary>
    /// Register a new user on Pixe.la with retry loop (free version is rate-limited)
    /// </summary>
    public async Task<(bool success, string? error)> RegisterUserAsync(string username, string token)
    {
        _logger.Information("Registering Pixe.la user: {Username}", username);

        const int maxRetries = 10;
        const int retryDelayMs = 500;
        var maxWaitTime = TimeSpan.FromSeconds(5);
        var startTime = DateTime.Now;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var payload = new { token, username, agreeTermsOfService = "yes", notMinor = "yes" };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(BaseUrl, content);
                var responseBody = await response.Content.ReadAsStringAsync();
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.Information("User registration succeeded on attempt {Attempt}", attempt);
                    return (true, null);
                }

                // User already exists - that's OK, they can use their existing account
                if (responseBody.Contains("already exist"))
                {
                    _logger.Information("User '{Username}' already exists, will use existing account", username);
                    return (true, null); // Treat as success - user exists
                }

                // Free Pixe.la rate limit - retry (response length 341 as per KEGOMODORO)
                if (responseBody.Length == 341 || response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    _logger.Warning("Pixe.la rate limited, attempt {Attempt}/{Max}, waiting...", attempt, maxRetries);
                    
                    if (DateTime.Now - startTime > maxWaitTime)
                    {
                        _logger.Warning("Max wait time exceeded");
                        return (false, "Pixe.la is busy. Please try again later.");
                    }
                    
                    await Task.Delay(retryDelayMs);
                    continue;
                }

                _logger.Warning("Registration failed: {Status} - {Body}", response.StatusCode, responseBody);
                return (false, $"Registration failed: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to register Pixe.la user, attempt {Attempt}", attempt);
                
                if (DateTime.Now - startTime > maxWaitTime)
                    return (false, ex.Message);
                
                await Task.Delay(retryDelayMs);
            }
        }

        return (false, "Could not connect to Pixe.la after retries");
    }

    public async Task<bool> CreateGraphAsync(User user, string graphId, string graphName)
    {
        _logger.Information("Creating Pixe.la graph: {GraphId}", graphId);

        try
        {
            var payload = new { id = graphId, name = graphName, unit = "hours", type = "float", color = "momiji", isEnablePng = true };
            var json = JsonSerializer.Serialize(payload);
            
            var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/{user.PixelaUsername}/graphs");
            request.Headers.Add("X-USER-TOKEN", user.PixelaToken);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            var success = response.IsSuccessStatusCode;
            
            _logger.Information("Graph creation {Status}", success ? "succeeded" : "failed");
            return success;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to create Pixe.la graph");
            return false;
        }
    }

    public async Task<bool> EnablePngAsync(User user)
    {
        if (!IsConfigured(user)) return false;

        _logger.Information("Enabling PNG for Pixe.la graph: {GraphId}", user.PixelaGraphId);

        try
        {
            var payload = new { isEnablePng = true };
            var json = JsonSerializer.Serialize(payload);
            
            var request = new HttpRequestMessage(HttpMethod.Put, $"{BaseUrl}/{user.PixelaUsername}/graphs/{user.PixelaGraphId}");
            request.Headers.Add("X-USER-TOKEN", user.PixelaToken);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            var success = response.IsSuccessStatusCode;
            
            if (!success)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.Warning("Failed to enable PNG: {Status} - {Body}", response.StatusCode, body);
            }
            
            return success;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to enable Pixe.la PNG format");
            return false;
        }
    }

    public async Task<double> GetPixelByDateAsync(User user, DateTime date)
    {
        if (!IsConfigured(user)) return 0;

        _logger.Information("Fetching pixel for {Date:yyyyMMdd}", date);

        int retryCount = 0;
        int maxRetries = 2;

        while (retryCount <= maxRetries)
        {
            try
            {
                var url = $"{BaseUrl}/users/{user.PixelaUsername}/graphs/{user.PixelaGraphId}/{date:yyyyMMdd}";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("X-USER-TOKEN", user.PixelaToken);

                var response = await _httpClient.SendAsync(request);
                
                if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable || 
                    (int)response.StatusCode == 503 || (int)response.StatusCode == 429)
                {
                    _logger.Warning("Pixe.la rate limit hit for pixel (Attempt {Attempt})", retryCount + 1);
                    retryCount++;
                    await Task.Delay(1000 * retryCount);
                    continue;
                }

                if (!response.IsSuccessStatusCode) return 0;

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("quantity", out var qtyProp))
                {
                    var qtyStr = qtyProp.GetString();
                    return double.TryParse(qtyStr, out var q) ? q : 0;
                }
                return 0;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to fetch pixel for date {Date} (Attempt {Attempt})", date, retryCount + 1);
                retryCount++;
                await Task.Delay(500);
            }
        }
        return 0;
    }

    public async Task<string> GetSvgAsync(User user, string? date = null, string? appearance = "dark")
    {
        if (!IsConfigured(user)) return "";

        _logger.Information("Fetching SVG for heatmap (appearance: {Appearance})", appearance);

        int retryCount = 0;
        int maxRetries = 2;

        while (retryCount <= maxRetries)
        {
            try
            {
                var queryParams = new List<string>();
                if (!string.IsNullOrEmpty(date)) queryParams.Add($"date={date}");
                if (!string.IsNullOrEmpty(appearance)) queryParams.Add($"appearance={appearance}");
                
                var url = $"{BaseUrl}/{user.PixelaUsername}/graphs/{user.PixelaGraphId}";
                if (queryParams.Count > 0)
                {
                    url += "?" + string.Join("&", queryParams);
                }
                
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("X-USER-TOKEN", user.PixelaToken);

                var response = await _httpClient.SendAsync(request);

                if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable || 
                    (int)response.StatusCode == 503 || (int)response.StatusCode == 429)
                {
                    _logger.Warning("Pixe.la rate limit hit for SVG (Attempt {Attempt})", retryCount + 1);
                    retryCount++;
                    await Task.Delay(1500 * retryCount);
                    continue;
                }

                if (!response.IsSuccessStatusCode) return "";

                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to fetch SVG heatmap (Attempt {Attempt})", retryCount + 1);
                retryCount++;
                await Task.Delay(500);
            }
        }
        return "";
    }

    public async Task<string?> GetLatestActiveDateAsync(User user)
    {
        if (!IsConfigured(user)) return null;

        _logger.Information("Searching for latest non-zero active date");

        try
        {
            // Fetch pixels for last 365 days and find the max date with quantity > 0
            var pixels = await GetPixelsAsync(user, DateTime.Today.AddDays(-365), DateTime.Today);
            var latestActive = pixels
                .Where(p => p.Quantity > 0)
                .OrderByDescending(p => p.Date)
                .FirstOrDefault();

            if (latestActive != null)
            {
                var dateStr = latestActive.Date.ToString("yyyyMMdd");
                _logger.Information("Latest active date found: {Date}", dateStr);
                return dateStr;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to find latest active date");
            return null;
        }
    }

    /// <summary>
    /// Post or update a pixel value for a specific date
    /// PUT /v1/users/{username}/graphs/{graphID}/{yyyyMMdd}
    /// </summary>
    public async Task<bool> PostPixelAsync(User user, DateTime date, double quantity)
    {
        if (!IsConfigured(user)) return false;

        var dateStr = date.ToString("yyyyMMdd");
        var url = $"{BaseUrl}/users/{user.PixelaUsername}/graphs/{user.PixelaGraphId}/{dateStr}";
        
        _logger.Information("Posting pixel: {Quantity}h on {Date} to {Url}", quantity, dateStr, url);

        try
        {
            var payload = new { quantity = quantity.ToString("F2") };
            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            var request = new HttpRequestMessage(HttpMethod.Put, url);
            request.Headers.Add("X-USER-TOKEN", user.PixelaToken);
            request.Content = content;

            var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                _logger.Information("Pixel posted successfully: {Date} = {Quantity}h", dateStr, quantity);
                return true;
            }
            else
            {
                _logger.Warning("Failed to post pixel: {Status} - {Body}", response.StatusCode, responseBody);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error posting pixel to Pixe.la");
            return false;
        }
    }

    /// <summary>
    /// Increment pixel value for a specific date (adds to existing value)
    /// PUT /v1/users/{username}/graphs/{graphID}/{yyyyMMdd}/increment
    /// </summary>
    public async Task<bool> IncrementPixelAsync(User user, DateTime date, double quantity)
    {
        if (!IsConfigured(user)) return false;

        // First get current value, then add to it
        var currentValue = await GetPixelByDateAsync(user, date);
        var newValue = currentValue + quantity;

        return await PostPixelAsync(user, date, newValue);
    }

    // DTOs for JSON deserialization
    private class PixelaStatsResponse
    {
        [JsonPropertyName("totalPixelsCount")]
        public int TotalPixelsCount { get; set; }
        
        [JsonPropertyName("totalQuantity")]
        public double TotalQuantity { get; set; }
        
        [JsonPropertyName("maxQuantity")]
        public double MaxQuantity { get; set; }
        
        [JsonPropertyName("minQuantity")]
        public double MinQuantity { get; set; }
        
        [JsonPropertyName("avgQuantity")]
        public double AvgQuantity { get; set; }
    }

    private class PixelaPixelsResponse
    {
        [JsonPropertyName("pixels")]
        public List<PixelData>? Pixels { get; set; }
    }

    private class PixelData
    {
        [JsonPropertyName("date")]
        public string Date { get; set; } = "";
        
        [JsonPropertyName("quantity")]
        public string Quantity { get; set; } = "0";
    }
}
