using System.Text.Json.Serialization;

namespace StravaWeather.Api.Models.DTOs
{
    public class StravaWebhookEventDto
    {
        [JsonPropertyName("object_type")]
        public string ObjectType { get; set; } = string.Empty;
        
        [JsonPropertyName("object_id")]
        public long ObjectId { get; set; }
        
        [JsonPropertyName("aspect_type")]
        public string AspectType { get; set; } = string.Empty;
        
        [JsonPropertyName("updates")]
        public Dictionary<string, object>? Updates { get; set; }
        
        [JsonPropertyName("owner_id")]
        public long OwnerId { get; set; }
        
        [JsonPropertyName("subscription_id")]
        public int SubscriptionId { get; set; }
        
        [JsonPropertyName("event_time")]
        public long EventTime { get; set; }
    }
    
    public class WebhookSubscription
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        
        [JsonPropertyName("callback_url")]
        public string CallbackUrl { get; set; } = string.Empty;
        
        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; } = string.Empty;
        
        [JsonPropertyName("updated_at")]
        public string UpdatedAt { get; set; } = string.Empty;
    }
}