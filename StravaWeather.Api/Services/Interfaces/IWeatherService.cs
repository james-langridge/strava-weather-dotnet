using StravaWeather.Api.Models.DTOs;

namespace StravaWeather.Api.Services.Interfaces
{
    public interface IWeatherService
    {
        Task<WeatherData> GetWeatherForActivityAsync(double lat, double lon, DateTime activityTime, string activityId);
        void ClearCache();
    }
}