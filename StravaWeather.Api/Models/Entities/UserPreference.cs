using System.ComponentModel.DataAnnotations;

namespace StravaWeather.Api.Models.Entities
{
    public class UserPreference
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        
        [Required]
        public Guid UserId { get; set; }
        
        [Required]
        [MaxLength(20)]
        public string TemperatureUnit { get; set; } = "fahrenheit";
        
        [Required]
        [MaxLength(20)]
        public string WeatherFormat { get; set; } = "detailed";
        
        public bool IncludeUvIndex { get; set; } = false;
        
        public bool IncludeVisibility { get; set; } = false;
        
        [MaxLength(500)]
        public string? CustomFormat { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation property
        public virtual User User { get; set; } = null!;
    }
}