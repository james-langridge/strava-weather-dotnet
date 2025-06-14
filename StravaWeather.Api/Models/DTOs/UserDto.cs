namespace StravaWeather.Api.Models.DTOs
{
    public class UserDto
    {
        public Guid Id { get; set; }
        public string StravaAthleteId { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string DisplayName => $"{FirstName} {LastName}".Trim();
        public string? ProfileImageUrl { get; set; }
        public string? Location { get; set; }
        public bool WeatherEnabled { get; set; }
        public UserPreferencesDto Preferences { get; set; } = new();
        public DateTime MemberSince { get; set; }
        public DateTime LastUpdated { get; set; }
    }
    
    public class UserPreferencesDto
    {
        public string TemperatureUnit { get; set; } = "fahrenheit";
        public string WeatherFormat { get; set; } = "detailed";
        public bool IncludeUvIndex { get; set; }
        public bool IncludeVisibility { get; set; }
        public string? CustomFormat { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
    
    public class UpdateUserDto
    {
        public bool? WeatherEnabled { get; set; }
    }
    
    public class UpdatePreferencesDto
    {
        public string? TemperatureUnit { get; set; }
        public string? WeatherFormat { get; set; }
        public bool? IncludeUvIndex { get; set; }
        public bool? IncludeVisibility { get; set; }
        public string? CustomFormat { get; set; }
    }
}