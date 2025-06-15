using System.Text.RegularExpressions;
using StravaWeather.Api.Data;
using StravaWeather.Api.Models.DTOs;
using StravaWeather.Api.Services.Interfaces;
using StravaWeather.Api.Utils;

namespace StravaWeather.Api.Services.Implementations
{
    public class ActivityProcessor : IActivityProcessor
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IStravaApiService _stravaApiService;
        private readonly IWeatherService _weatherService;
        private readonly ILogger<ActivityProcessor> _logger;
        
        private static readonly Regex[] WeatherPatterns = 
        {
            new Regex(@"°C", RegexOptions.Compiled),
            new Regex(@"°F", RegexOptions.Compiled),
            new Regex(@"Feels like", RegexOptions.Compiled),
            new Regex(@"Humidity", RegexOptions.Compiled),
            new Regex(@"m\/s from", RegexOptions.Compiled),
            new Regex(@"🌤️ Weather:", RegexOptions.Compiled),
            new Regex(@"Weather:", RegexOptions.Compiled)
        };
        
        public ActivityProcessor(
            ApplicationDbContext dbContext,
            IStravaApiService stravaApiService,
            IWeatherService weatherService,
            ILogger<ActivityProcessor> logger)
        {
            _dbContext = dbContext;
            _stravaApiService = stravaApiService;
            _weatherService = weatherService;
            _logger = logger;
        }
        
        public async Task<ProcessingResult> ProcessActivityAsync(string activityId, Guid userId)
        {
            try
            {
                _logger.LogInformation("Processing activity {ActivityId} for user {UserId}", activityId, userId);
                
                var user = await _dbContext.Users.FindAsync(userId);
                
                if (user == null)
                {
                    _logger.LogError("User {UserId} not found", userId);
                    return new ProcessingResult
                    {
                        Success = false,
                        ActivityId = activityId,
                        Error = "User not found"
                    };
                }
                
                if (!user.WeatherEnabled)
                {
                    _logger.LogInformation("Weather updates disabled for user {UserId}", userId);
                    return new ProcessingResult
                    {
                        Success = false,
                        ActivityId = activityId,
                        Skipped = true,
                        Reason = "Weather updates disabled"
                    };
                }
                
                // Ensure valid Strava token
                var tokenData = await _stravaApiService.EnsureValidTokenAsync(
                    user.AccessToken,
                    user.RefreshToken,
                    user.TokenExpiresAt);
                
                // Update tokens if refreshed
                if (tokenData.WasRefreshed)
                {
                    user.AccessToken = tokenData.AccessToken;
                    user.RefreshToken = tokenData.RefreshToken;
                    user.TokenExpiresAt = tokenData.ExpiresAt;
                    user.UpdatedAt = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync();
                }
                
                // Get activity details from Strava
                var activity = await _stravaApiService.GetActivityAsync(activityId, tokenData.AccessToken);
                
                if (activity == null)
                {
                    _logger.LogError("Activity {ActivityId} not found on Strava", activityId);
                    return new ProcessingResult
                    {
                        Success = false,
                        ActivityId = activityId,
                        Error = "Activity not found on Strava"
                    };
                }
                
                // Check if activity already has weather data
                if (HasWeatherData(activity.Description))
                {
                    _logger.LogInformation("Activity {ActivityId} already has weather data", activityId);
                    return new ProcessingResult
                    {
                        Success = true,
                        ActivityId = activityId,
                        Skipped = true,
                        Reason = "Already has weather data"
                    };
                }
                
                // Check if activity has GPS coordinates
                if (activity.StartLatlng == null || activity.StartLatlng.Length != 2)
                {
                    _logger.LogWarning("Activity {ActivityId} has no GPS coordinates", activityId);
                    return new ProcessingResult
                    {
                        Success = false,
                        ActivityId = activityId,
                        Skipped = true,
                        Reason = "No GPS coordinates"
                    };
                }
                
                var lat = activity.StartLatlng[0];
                var lon = activity.StartLatlng[1];
                
                // Get weather data
                var weatherData = await _weatherService.GetWeatherForActivityAsync(
                    lat, lon, activity.StartDate, activityId);
                
                // Create updated description with weather
                var updatedDescription = CreateWeatherDescription(activity, weatherData);
                
                // Update activity on Strava
                await _stravaApiService.UpdateActivityAsync(activityId, tokenData.AccessToken, new StravaUpdateData
                {
                    Description = updatedDescription
                });
                
                _logger.LogInformation("Activity {ActivityId} updated with weather data", activityId);
                
                return new ProcessingResult
                {
                    Success = true,
                    ActivityId = activityId,
                    WeatherData = weatherData
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing activity {ActivityId}", activityId);
                return new ProcessingResult
                {
                    Success = false,
                    ActivityId = activityId,
                    Error = ex.Message
                };
            }
        }
        
        private bool HasWeatherData(string? description)
        {
            if (string.IsNullOrEmpty(description))
                return false;
                
            return WeatherPatterns.Any(pattern => pattern.IsMatch(description));
        }
        
        private string CreateWeatherDescription(StravaActivity activity, WeatherData weatherData)
        {
            var originalDescription = activity.Description ?? "";
            
            // Format weather conditions
            var condition = CapitalizeFirst(weatherData.Description);
            
            // Build weather line
            var weatherLine = FormatWeatherLine(condition, weatherData);
            
            if (!string.IsNullOrEmpty(originalDescription))
            {
                return $"{originalDescription}\n\n{weatherLine}";
            }
            
            return weatherLine;
        }
        
        private string FormatWeatherLine(string condition, WeatherData weatherData)
        {
            var parts = new[]
            {
                condition,
                $"{weatherData.Temperature}°C",
                $"Feels like {weatherData.TemperatureFeel}°C",
                $"Humidity {weatherData.Humidity}%",
                $"Wind {weatherData.WindSpeed}m/s from {GetWindDirectionString(weatherData.WindDirection)}"
            };
            
            return string.Join(", ", parts);
        }
        
        private string GetWindDirectionString(int degrees)
        {
            // Normalize degrees to 0-360 range
            degrees = ((degrees % 360) + 360) % 360;
            var index = (int)Math.Round(degrees / 22.5) % 16;
            return Constants.WindDirections.Compass[index];
        }
        
        private string CapitalizeFirst(string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;
                
            return char.ToUpper(str[0]) + str.Substring(1);
        }
    }
}