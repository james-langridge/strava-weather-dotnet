namespace StravaWeather.Api.Models.DTOs
{
    public class WeatherData
    {
        public double Temperature { get; set; }
        public double TemperatureFeel { get; set; }
        public int Humidity { get; set; }
        public int Pressure { get; set; }
        public double WindSpeed { get; set; }
        public int WindDirection { get; set; }
        public double? WindGust { get; set; }
        public int CloudCover { get; set; }
        public int Visibility { get; set; }
        public string Condition { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public double? UvIndex { get; set; }
        public string Timestamp { get; set; } = string.Empty;
    }
    
    public class ProcessingResult
    {
        public bool Success { get; set; }
        public string ActivityId { get; set; } = string.Empty;
        public WeatherData? WeatherData { get; set; }
        public string? Error { get; set; }
        public bool Skipped { get; set; }
        public string? Reason { get; set; }
    }
}