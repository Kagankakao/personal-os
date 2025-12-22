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
public class PixelaService : IPixelaService
{
    private readonly ILogger _logger = Log.ForContext<PixelaService>();
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://pixe.la/v1/users";

    public PixelaService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _logger.Debug("PixelaService initialized");
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
            var url = $"{BaseUrl}/{user.PixelaUsername}/graphs/{user.PixelaGraphId}/pixels";
            
            if (from.HasValue)
                url += $"?from={from.Value:yyyyMMdd}";
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

    public async Task<bool> RegisterUserAsync(string username, string token)
    {
        _logger.Information("Registering Pixe.la user: {Username}", username);

        try
        {
            var payload = new { token, username, agreeTermsOfService = "yes", notMinor = "yes" };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(BaseUrl, content);
            var success = response.IsSuccessStatusCode;
            
            _logger.Information("User registration {Status}", success ? "succeeded" : "failed");
            return success;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to register Pixe.la user");
            return false;
        }
    }

    public async Task<bool> CreateGraphAsync(User user, string graphId, string graphName)
    {
        _logger.Information("Creating Pixe.la graph: {GraphId}", graphId);

        try
        {
            var payload = new { id = graphId, name = graphName, unit = "hours", type = "float", color = "momiji" };
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
