namespace StravaWeather.Api.Configuration
{
    public class StravaOptions
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string WebhookVerifyToken { get; set; } = string.Empty;
    }
    
    public class WeatherOptions
    {
        public string ApiKey { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = "https://api.openweathermap.org/data/3.0/onecall";
    }
}