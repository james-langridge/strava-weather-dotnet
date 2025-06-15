using StravaWeather.Api.Models.DTOs;

namespace StravaWeather.Api.Services.Interfaces
{
    public interface IWebhookSubscriptionService
    {
        Task<WebhookSubscription?> ViewSubscriptionAsync();
        Task<WebhookSubscription> CreateSubscriptionAsync(string callbackUrl);
        Task DeleteSubscriptionAsync(int subscriptionId);
        Task<bool> VerifyEndpointAsync(string callbackUrl);
        Task EnsureSubscriptionExistsAsync();
    }
}