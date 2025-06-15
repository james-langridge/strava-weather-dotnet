using FluentValidation;
using StravaWeather.Api.Models.DTOs;

namespace StravaWeather.Api.Validation
{
    public class UpdateUserDtoValidator : AbstractValidator<UpdateUserDto>
    {
        public UpdateUserDtoValidator()
        {
            RuleFor(x => x.WeatherEnabled)
                .NotNull()
                .When(x => x.WeatherEnabled.HasValue)
                .WithMessage("Weather enabled must be true or false");
        }
    }
    
    public class UpdatePreferencesDtoValidator : AbstractValidator<UpdatePreferencesDto>
    {
        public UpdatePreferencesDtoValidator()
        {
            RuleFor(x => x.TemperatureUnit)
                .Must(x => x == "fahrenheit" || x == "celsius")
                .When(x => !string.IsNullOrEmpty(x.TemperatureUnit))
                .WithMessage("Temperature unit must be 'fahrenheit' or 'celsius'");
                
            RuleFor(x => x.WeatherFormat)
                .Must(x => x == "detailed" || x == "simple")
                .When(x => !string.IsNullOrEmpty(x.WeatherFormat))
                .WithMessage("Weather format must be 'detailed' or 'simple'");
                
            RuleFor(x => x.CustomFormat)
                .MaximumLength(500)
                .When(x => x.CustomFormat != null)
                .WithMessage("Custom format must not exceed 500 characters");
                
            RuleFor(x => x)
                .Must(x => 
                    x.TemperatureUnit != null || 
                    x.WeatherFormat != null || 
                    x.IncludeUvIndex != null || 
                    x.IncludeVisibility != null || 
                    x.CustomFormat != null)
                .WithMessage("At least one preference field must be provided");
        }
    }
}