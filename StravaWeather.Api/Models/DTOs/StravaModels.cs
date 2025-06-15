using System.Text.Json.Serialization;

namespace StravaWeather.Api.Models.DTOs
{
    public class StravaActivity
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }
        
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        
        [JsonPropertyName("distance")]
        public double Distance { get; set; }
        
        [JsonPropertyName("moving_time")]
        public int MovingTime { get; set; }
        
        [JsonPropertyName("elapsed_time")]
        public int ElapsedTime { get; set; }
        
        [JsonPropertyName("total_elevation_gain")]
        public double TotalElevationGain { get; set; }
        
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
        
        [JsonPropertyName("start_date")]
        public DateTime StartDate { get; set; }
        
        [JsonPropertyName("start_date_local")]
        public DateTime StartDateLocal { get; set; }
        
        [JsonPropertyName("timezone")]
        public string Timezone { get; set; } = string.Empty;
        
        [JsonPropertyName("start_latlng")]
        public double[]? StartLatlng { get; set; }
        
        [JsonPropertyName("end_latlng")]
        public double[]? EndLatlng { get; set; }
        
        [JsonPropertyName("achievement_count")]
        public int AchievementCount { get; set; }
        
        [JsonPropertyName("kudos_count")]
        public int KudosCount { get; set; }
        
        [JsonPropertyName("comment_count")]
        public int CommentCount { get; set; }
        
        [JsonPropertyName("athlete_count")]
        public int AthleteCount { get; set; }
        
        [JsonPropertyName("photo_count")]
        public int PhotoCount { get; set; }
        
        [JsonPropertyName("description")]
        public string? Description { get; set; }
        
        [JsonPropertyName("private")]
        public bool Private { get; set; }
        
        [JsonPropertyName("visibility")]
        public string Visibility { get; set; } = string.Empty;
    }
    
    public class StravaUpdateData
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        
        [JsonPropertyName("type")]
        public string? Type { get; set; }
        
        [JsonPropertyName("description")]
        public string? Description { get; set; }
        
        [JsonPropertyName("gear_id")]
        public string? GearId { get; set; }
        
        [JsonPropertyName("trainer")]
        public bool? Trainer { get; set; }
        
        [JsonPropertyName("commute")]
        public bool? Commute { get; set; }
    }
    
    public class StravaAthlete
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }
        
        [JsonPropertyName("firstname")]
        public string? FirstName { get; set; }
        
        [JsonPropertyName("lastname")]
        public string? LastName { get; set; }
        
        [JsonPropertyName("profile_medium")]
        public string? ProfileMedium { get; set; }
        
        [JsonPropertyName("profile")]
        public string? Profile { get; set; }
        
        [JsonPropertyName("city")]
        public string? City { get; set; }
        
        [JsonPropertyName("state")]
        public string? State { get; set; }
        
        [JsonPropertyName("country")]
        public string? Country { get; set; }
    }
    
    public class StravaTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;
        
        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = string.Empty;
        
        [JsonPropertyName("expires_at")]
        public long ExpiresAt { get; set; }
        
        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
        
        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = string.Empty;
        
        [JsonPropertyName("athlete")]
        public StravaAthlete? Athlete { get; set; }
    }
    
    public class TokenRefreshResult
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }
    
    public class TokenValidationResult
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public bool WasRefreshed { get; set; }
    }
}   