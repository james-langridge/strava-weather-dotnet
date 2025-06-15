using FluentValidation;
using FluentValidation.AspNetCore;
using StravaWeather.Api.Configuration;
using StravaWeather.Api.Services.Implementations;
using StravaWeather.Api.Services.Interfaces;
using StravaWeather.Api.Utils;

namespace StravaWeather.Api.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Register services
            services.AddSingleton<IEncryptionService, EncryptionService>();
            services.AddScoped<IStravaApiService, StravaApiService>();
            services.AddScoped<IWeatherService, WeatherService>();
            services.AddScoped<IActivityProcessor, ActivityProcessor>();
            services.AddScoped<IWebhookSubscriptionService, WebhookSubscriptionService>();
            
            // Register helpers
            services.AddScoped<JwtHelper>();
            
            // Configure options
            services.Configure<StravaOptions>(configuration.GetSection("Strava"));
            services.Configure<WeatherOptions>(configuration.GetSection("Weather"));
            
            // Add FluentValidation
            services.AddFluentValidationAutoValidation();
            services.AddFluentValidationClientsideAdapters();
            services.AddValidatorsFromAssemblyContaining<Program>();
            
            // Add HttpClient with Polly
            services.AddHttpClient("Strava", client =>
            {
                client.BaseAddress = new Uri("https://www.strava.com/api/v3/");
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            });
            
            services.AddHttpClient("Weather", client =>
            {
                client.BaseAddress = new Uri("https://api.openweathermap.org/");
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            });
            
            return services;
        }
    }
}