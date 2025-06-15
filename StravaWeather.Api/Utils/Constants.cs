namespace StravaWeather.Api.Utils
{
    public static class Constants
    {
        public static class Weather
        {
            public const int CacheExpiryMinutes = 30;
            public const int CoordinatePrecision = 4;
            public const int TimeRoundMinutes = 15;
            public const int HistoricalLimitHours = 120;
            public const int RecentActivityThresholdHours = 1;
            public const int ApiTimeoutMs = 5000;
            public const int DefaultVisibilityMeters = 10000;
        }
        
        public static class Webhook
        {
            public const int MaxProcessingTimeMs = 8000;
            public const int MaxRetryAttempts = 3;
            public static readonly int[] RetryDelaysMs = { 1500, 3000 };
        }
        
        public static class Jwt
        {
            public const string CookieName = "strava-weather-session";
            public const int ExpiryDays = 30;
            public const string Issuer = "strava-weather-api";
            public const string Audience = "strava-weather-client";
        }
        
        public static class WindDirections
        {
            public static readonly string[] Compass = 
            {
                "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE",
                "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW"
            };
        }
    }
}