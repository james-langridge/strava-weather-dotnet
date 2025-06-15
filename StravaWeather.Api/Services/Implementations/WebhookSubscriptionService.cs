using System.Text.Json;
using StravaWeather.Api.Models.DTOs;
using StravaWeather.Api.Services.Interfaces;

namespace StravaWeather.Api.Services.Implementations
{
    public class WebhookSubscriptionService : IWebhookSubscriptionService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<WebhookSubscriptionService> _logger;
        private const string SubscriptionUrl = "https://www.strava.com/api/v3/push_subscriptions";
        
        public WebhookSubscriptionService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<WebhookSubscriptionService> logger)
        {
            _httpClient = httpClientFactory.CreateClient();
            _configuration = configuration;
            _logger = logger;
        }
        
        public async Task<WebhookSubscription?> ViewSubscriptionAsync()
        {
            _logger.LogDebug("Checking for existing webhook subscription");
            
            try
            {
                var url = $"{SubscriptionUrl}?" +
                    $"client_id={_configuration["STRAVA_CLIENT_ID"]}&" +
                    $"client_secret={_configuration["STRAVA_CLIENT_SECRET"]}";
                
                var response = await _httpClient.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorText = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to retrieve webhook subscription: {Status} - {Error}", 
                        response.StatusCode, errorText);
                    throw new InvalidOperationException($"Failed to view subscription: {response.StatusCode}");
                }
                
                var json = await response.Content.ReadAsStringAsync();
                var subscriptions = JsonSerializer.Deserialize<List<WebhookSubscription>>(json);
                
                if (subscriptions != null && subscriptions.Count > 0)
                {
                    var subscription = subscriptions[0];
                    _logger.LogInformation("Found existing webhook subscription: {Id} - {CallbackUrl}", 
                        subscription.Id, subscription.CallbackUrl);
                    return subscription;
                }
                
                _logger.LogDebug("No existing webhook subscription found");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving webhook subscription");
                throw;
            }
        }
        
        public async Task<WebhookSubscription> CreateSubscriptionAsync(string callbackUrl)
        {
            _logger.LogInformation("Creating webhook subscription with callback URL: {CallbackUrl}", callbackUrl);
            
            try
            {
                // Verify no existing subscription
                var existing = await ViewSubscriptionAsync();
                if (existing != null)
                {
                    _logger.LogWarning("Cannot create subscription: one already exists with ID {Id}", existing.Id);
                    throw new InvalidOperationException("Subscription already exists. Delete existing subscription first.");
                }
                
                // Validate callback URL
                if (!callbackUrl.StartsWith("https://"))
                {
                    _logger.LogError("Invalid callback URL: must use HTTPS - {CallbackUrl}", callbackUrl);
                    throw new ArgumentException("Callback URL must use HTTPS protocol");
                }
                
                var formData = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("client_id", _configuration["STRAVA_CLIENT_ID"]!),
                    new KeyValuePair<string, string>("client_secret", _configuration["STRAVA_CLIENT_SECRET"]!),
                    new KeyValuePair<string, string>("callback_url", callbackUrl),
                    new KeyValuePair<string, string>("verify_token", _configuration["STRAVA_WEBHOOK_VERIFY_TOKEN"]!)
                });
                
                var response = await _httpClient.PostAsync(SubscriptionUrl, formData);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorText = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to create webhook subscription: {Status} - {Error}", 
                        response.StatusCode, errorText);
                    throw new InvalidOperationException($"Failed to create subscription: {errorText}");
                }
                
                var json = await response.Content.ReadAsStringAsync();
                var subscription = JsonSerializer.Deserialize<WebhookSubscription>(json)!;
                
                _logger.LogInformation("Webhook subscription created successfully: {Id}", subscription.Id);
                
                return subscription;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating webhook subscription");
                throw;
            }
        }
        
        public async Task DeleteSubscriptionAsync(int subscriptionId)
        {
            _logger.LogInformation("Deleting webhook subscription {Id}", subscriptionId);
            
            try
            {
                var url = $"{SubscriptionUrl}/{subscriptionId}?" +
                    $"client_id={_configuration["STRAVA_CLIENT_ID"]}&" +
                    $"client_secret={_configuration["STRAVA_CLIENT_SECRET"]}";
                
                var response = await _httpClient.DeleteAsync(url);
                
                // 204 No Content is success for DELETE
                if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    _logger.LogInformation("Webhook subscription deleted successfully");
                    return;
                }
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorText = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to delete webhook subscription: {Status} - {Error}", 
                        response.StatusCode, errorText);
                    throw new InvalidOperationException($"Failed to delete subscription: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting webhook subscription");
                throw;
            }
        }
        
        public async Task<bool> VerifyEndpointAsync(string callbackUrl)
        {
            _logger.LogDebug("Verifying webhook endpoint accessibility: {CallbackUrl}", callbackUrl);
            
            var testChallenge = $"test_challenge_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            
            try
            {
                var testUrl = $"{callbackUrl}?" +
                    $"hub.mode=subscribe&" +
                    $"hub.verify_token={_configuration["STRAVA_WEBHOOK_VERIFY_TOKEN"]}&" +
                    $"hub.challenge={testChallenge}";
                
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var response = await _httpClient.GetAsync(testUrl, cts.Token);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Webhook endpoint returned non-OK status: {Status}", response.StatusCode);
                    return false;
                }
                
                var json = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                
                if (data != null && data.TryGetValue("hub.challenge", out var returnedChallenge) && 
                    returnedChallenge == testChallenge)
                {
                    _logger.LogInformation("Webhook endpoint verified successfully");
                    return true;
                }
                
                _logger.LogWarning("Webhook endpoint returned incorrect challenge");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to verify webhook endpoint");
                return false;
            }
        }
        
        public async Task EnsureSubscriptionExistsAsync()
        {
            try
            {
                var existing = await ViewSubscriptionAsync();
                
                if (existing != null)
                {
                    _logger.LogInformation("Webhook subscription already exists: {Id}", existing.Id);
                    return;
                }
                
                var callbackUrl = $"{_configuration["APP_URL"]}/api/strava/webhook";
                
                _logger.LogInformation("No webhook subscription found, creating new subscription");
                
                var isAccessible = await VerifyEndpointAsync(callbackUrl);
                
                if (!isAccessible)
                {
                    _logger.LogError("Webhook endpoint is not accessible: {CallbackUrl}", callbackUrl);
                    return;
                }
                
                await CreateSubscriptionAsync(callbackUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ensure webhook subscription exists");
            }
        }
    }
}