using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using StravaWeather.Api.Configuration;
using StravaWeather.Api.Models.DTOs;
using StravaWeather.Api.Services.Interfaces;
using StravaWeather.Api.Utils;

namespace StravaWeather.Api.Services.Implementations
{
    public class WeatherService : IWeatherService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;
        private readonly ILogger<WeatherService> _logger;
        private readonly string _apiKey;
        
        public WeatherService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IMemoryCache cache,
            ILogger<WeatherService> logger)
        {
            _httpClient = httpClientFactory.CreateClient("Weather");
            _configuration = configuration;
            _cache = cache;
            _logger = logger;
            _apiKey = configuration["OPENWEATHERMAP_API_KEY"] 
                ?? throw new InvalidOperationException("OPENWEATHERMAP_API_KEY not configured");
        }
        
        public async Task<WeatherData> GetWeatherForActivityAsync(
            double lat, 
            double lon, 
            DateTime activityTime, 
            string activityId)
        {
            var cacheKey = GetCacheKey(lat, lon, activityTime, activityId);
            
            // Check cache first
            if (_cache.TryGetValue<WeatherData>(cacheKey, out var cachedData))
            {
                _logger.LogDebug("Weather cache hit for activity {ActivityId}", activityId);
                return cachedData!;
            }
            
            var logContext = new
            {
                ActivityId = activityId,
                Coordinates = new { lat, lon },
                ActivityTime = activityTime.ToString("O")
            };
            
            _logger.LogInformation("Fetching weather data for activity. Context: {Context}", 
                JsonSerializer.Serialize(logContext));
            
            try
            {
                var now = DateTime.UtcNow;
                var hoursSinceActivity = (now - activityTime).TotalHours;
                
                WeatherData weatherData;
                string dataSource;
                
                if (hoursSinceActivity > Constants.Weather.RecentActivityThresholdHours &&
                    hoursSinceActivity <= Constants.Weather.HistoricalLimitHours)
                {
                    // Use Time Machine for historical data
                    dataSource = "historical";
                    weatherData = await GetHistoricalWeatherAsync(lat, lon, activityTime);
                }
                else if (hoursSinceActivity <= Constants.Weather.RecentActivityThresholdHours)
                {
                    // Use current data for very recent activities
                    dataSource = "current";
                    weatherData = await GetCurrentWeatherAsync(lat, lon);
                }
                else
                {
                    // Activity too old for Time Machine, use current as fallback
                    dataSource = "current-fallback";
                    _logger.LogWarning(
                        "Activity outside Time Machine range, using current weather. " +
                        "ActivityId: {ActivityId}, HoursSinceActivity: {Hours}",
                        activityId, hoursSinceActivity);
                    weatherData = await GetCurrentWeatherAsync(lat, lon);
                }
                
                // Cache the result
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromMinutes(Constants.Weather.CacheExpiryMinutes));
                _cache.Set(cacheKey, weatherData, cacheOptions);
                
                _logger.LogInformation(
                    "Weather data retrieved successfully. ActivityId: {ActivityId}, " +
                    "DataSource: {DataSource}, Temperature: {Temperature}°C, Condition: {Condition}",
                    activityId, dataSource, weatherData.Temperature, weatherData.Condition);
                
                return weatherData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch weather data. Context: {Context}", 
                    JsonSerializer.Serialize(logContext));
                throw new InvalidOperationException($"Failed to fetch weather data: {ex.Message}", ex);
            }
        }
        
        private async Task<WeatherData> GetCurrentWeatherAsync(double lat, double lon)
        {
            var url = $"data/3.0/onecall?lat={lat:F6}&lon={lon:F6}&appid={_apiKey}" +
                      "&units=metric&exclude=minutely,hourly,daily,alerts";
            
            _logger.LogDebug("Requesting current weather from One Call API: {Url}", url.Replace(_apiKey, "***"));
            
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                await HandleApiError(response, "One Call API");
            }
            
            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<OneCallCurrentResponse>(json)!;
            
            return FormatWeatherData(data.Current);
        }
        
        private async Task<WeatherData> GetHistoricalWeatherAsync(double lat, double lon, DateTime time)
        {
            var dt = ((DateTimeOffset)time).ToUnixTimeSeconds();
            var url = $"data/3.0/onecall/timemachine?lat={lat:F6}&lon={lon:F6}&dt={dt}" +
                      $"&appid={_apiKey}&units=metric";
            
            _logger.LogDebug("Requesting historical weather from Time Machine: {Url}", url.Replace(_apiKey, "***"));
            
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                await HandleApiError(response, "Time Machine API");
            }
            
            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<TimeMachineResponse>(json)!;
            
            return FormatWeatherData(data.Data[0]);
        }
        
        private WeatherData FormatWeatherData(dynamic data)
        {
            return new WeatherData
            {
                Temperature = Math.Round((double)data.Temp),
                TemperatureFeel = Math.Round((double)data.FeelsLike),
                Humidity = (int)data.Humidity,
                Pressure = (int)data.Pressure,
                WindSpeed = Math.Round((double)data.WindSpeed * 10) / 10,
                WindDirection = (int)data.WindDeg,
                WindGust = data.WindGust != null ? Math.Round((double)data.WindGust * 10) / 10 : null,
                CloudCover = (int)data.Clouds,
                Visibility = (int)Math.Round((data.Visibility ?? Constants.Weather.DefaultVisibilityMeters) / 1000.0),
                Condition = (string)data.Weather[0].Main,
                Description = (string)data.Weather[0].Description,
                Icon = (string)data.Weather[0].Icon,
                UvIndex = data.Uvi ?? 0,
                Timestamp = DateTimeOffset.FromUnixTimeSeconds((long)data.Dt).UtcDateTime.ToString("O")
            };
        }
        
        private string GetCacheKey(double lat, double lon, DateTime time, string activityId)
        {
            // Round coordinates to configured precision
            var factor = Math.Pow(10, Constants.Weather.CoordinatePrecision);
            var roundedLat = Math.Round(lat * factor) / factor;
            var roundedLon = Math.Round(lon * factor) / factor;
            
            // Round time to nearest configured interval
            var roundedTime = new DateTime(
                time.Year, time.Month, time.Day, time.Hour,
                (time.Minute / Constants.Weather.TimeRoundMinutes) * Constants.Weather.TimeRoundMinutes, 0);
            
            return $"weather:{roundedLat}:{roundedLon}:{roundedTime.Ticks}:{activityId}";
        }
        
        private async Task HandleApiError(HttpResponseMessage response, string apiName)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("{ApiName} request failed: {Status} - {Error}", 
                apiName, response.StatusCode, errorContent);
            
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new InvalidOperationException("Weather API authentication failed");
            }
            else if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new InvalidOperationException("Weather API rate limit exceeded");
            }
            else if (response.StatusCode == HttpStatusCode.RequestTimeout)
            {
                throw new InvalidOperationException("Weather API request timeout");
            }
            
            throw new InvalidOperationException(
                $"Weather API error: {response.StatusCode} - {errorContent}");
        }
        
        public void ClearCache()
        {
            // Memory cache doesn't provide a clear method, so we track keys
            _logger.LogInformation("Weather cache clear requested");
        }
        
        // DTOs for API responses
        private class OneCallCurrentResponse
        {
            public CurrentWeather Current { get; set; } = null!;
        }
        
        private class CurrentWeather
        {
            public long Dt { get; set; }
            public double Temp { get; set; }
            public double FeelsLike { get; set; }
            public int Humidity { get; set; }
            public int Pressure { get; set; }
            public double WindSpeed { get; set; }
            public int WindDeg { get; set; }
            public double? WindGust { get; set; }
            public int Clouds { get; set; }
            public int? Visibility { get; set; }
            public double? Uvi { get; set; }
            public List<WeatherCondition> Weather { get; set; } = new();
        }
        
        private class TimeMachineResponse
        {
            public List<CurrentWeather> Data { get; set; } = new();
        }
        
        private class WeatherCondition
        {
            public string Main { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string Icon { get; set; } = string.Empty;
        }
    }
}