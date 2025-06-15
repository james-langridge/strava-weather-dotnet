using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using StravaWeather.Api.Configuration;
using StravaWeather.Api.Models.DTOs;
using StravaWeather.Api.Services.Interfaces;

namespace StravaWeather.Api.Services.Implementations
{
    public class StravaApiService : IStravaApiService
    {
        private readonly HttpClient _httpClient;
        private readonly IEncryptionService _encryptionService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<StravaApiService> _logger;
        private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;
        
        private const string BaseUrl = "https://www.strava.com/api/v3";
        private const string TokenUrl = "https://www.strava.com/oauth/token";
        private const int TokenRefreshBufferMinutes = 5;
        
        public StravaApiService(
            IHttpClientFactory httpClientFactory,
            IEncryptionService encryptionService,
            IConfiguration configuration,
            ILogger<StravaApiService> logger)
        {
            _httpClient = httpClientFactory.CreateClient("Strava");
            _encryptionService = encryptionService;
            _configuration = configuration;
            _logger = logger;
            
            // Configure retry policy with exponential backoff
            _retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => msg.StatusCode == HttpStatusCode.TooManyRequests)
                .WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        _logger.LogWarning("Retry {RetryCount} after {Delay}ms", retryCount, timespan.TotalMilliseconds);
                    });
        }
        
        public async Task<StravaActivity?> GetActivityAsync(string activityId, string encryptedAccessToken)
        {
            _logger.LogDebug("Fetching activity {ActivityId} from Strava", activityId);
            
            try
            {
                var accessToken = _encryptionService.Decrypt(encryptedAccessToken);
                
                var request = new HttpRequestMessage(HttpMethod.Get, $"activities/{activityId}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                
                var response = await _retryPolicy.ExecuteAsync(async () => 
                    await _httpClient.SendAsync(request));
                
                if (!response.IsSuccessStatusCode)
                {
                    await HandleApiError(response, "GetActivity", new { activityId });
                }
                
                var json = await response.Content.ReadAsStringAsync();
                var activity = JsonSerializer.Deserialize<StravaActivity>(json);
                
                _logger.LogInformation("Activity retrieved successfully: {ActivityId}, Name: {ActivityName}", 
                    activityId, activity?.Name);
                
                return activity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch activity {ActivityId}", activityId);
                throw;
            }
        }
        
        public async Task<StravaActivity> UpdateActivityAsync(
            string activityId, 
            string encryptedAccessToken, 
            StravaUpdateData updateData)
        {
            _logger.LogDebug("Updating activity {ActivityId} on Strava", activityId);
            
            try
            {
                var accessToken = _encryptionService.Decrypt(encryptedAccessToken);
                
                var request = new HttpRequestMessage(HttpMethod.Put, $"activities/{activityId}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                request.Content = new StringContent(
                    JsonSerializer.Serialize(updateData),
                    Encoding.UTF8,
                    "application/json");
                
                var response = await _retryPolicy.ExecuteAsync(async () => 
                    await _httpClient.SendAsync(request));
                
                if (!response.IsSuccessStatusCode)
                {
                    await HandleApiError(response, "UpdateActivity", new { activityId });
                }
                
                var json = await response.Content.ReadAsStringAsync();
                var updatedActivity = JsonSerializer.Deserialize<StravaActivity>(json)!;
                
                _logger.LogInformation("Activity updated successfully: {ActivityId}", activityId);
                
                return updatedActivity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update activity {ActivityId}", activityId);
                throw;
            }
        }
        
        public async Task<TokenRefreshResult> RefreshAccessTokenAsync(string encryptedRefreshToken)
        {
            _logger.LogDebug("Refreshing Strava access token");
            
            try
            {
                var refreshToken = _encryptionService.Decrypt(encryptedRefreshToken);
                
                var requestBody = new Dictionary<string, string>
                {
                    ["client_id"] = _configuration["STRAVA_CLIENT_ID"]!,
                    ["client_secret"] = _configuration["STRAVA_CLIENT_SECRET"]!,
                    ["refresh_token"] = refreshToken,
                    ["grant_type"] = "refresh_token"
                };
                
                var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl)
                {
                    Content = new FormUrlEncodedContent(requestBody)
                };
                
                var response = await _httpClient.SendAsync(request);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Token refresh failed: {Status} - {Error}", 
                        response.StatusCode, errorContent);
                    throw new InvalidOperationException($"Token refresh failed ({response.StatusCode}): {errorContent}");
                }
                
                var json = await response.Content.ReadAsStringAsync();
                var tokenData = JsonSerializer.Deserialize<StravaTokenResponse>(json)!;
                
                _logger.LogInformation("Access token refreshed successfully, expires at {ExpiresAt}", 
                    DateTimeOffset.FromUnixTimeSeconds(tokenData.ExpiresAt));
                
                return new TokenRefreshResult
                {
                    AccessToken = tokenData.AccessToken,
                    RefreshToken = tokenData.RefreshToken,
                    ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(tokenData.ExpiresAt).UtcDateTime
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh access token");
                throw;
            }
        }
        
        public async Task<TokenValidationResult> EnsureValidTokenAsync(
            string encryptedAccessToken,
            string encryptedRefreshToken,
            DateTime expiresAt)
        {
            var now = DateTime.UtcNow;
            var bufferTime = now.AddMinutes(TokenRefreshBufferMinutes);
            
            // Check if token needs refresh
            if (expiresAt <= bufferTime)
            {
                var timeUntilExpiry = expiresAt - now;
                
                _logger.LogInformation("Access token expiring soon, refreshing. Time until expiry: {TimeUntilExpiry}", 
                    timeUntilExpiry);
                
                var tokenData = await RefreshAccessTokenAsync(encryptedRefreshToken);
                
                // Encrypt new tokens before returning
                return new TokenValidationResult
                {
                    AccessToken = _encryptionService.Encrypt(tokenData.AccessToken),
                    RefreshToken = _encryptionService.Encrypt(tokenData.RefreshToken),
                    ExpiresAt = tokenData.ExpiresAt,
                    WasRefreshed = true
                };
            }
            
            _logger.LogDebug("Access token still valid, expires at {ExpiresAt}", expiresAt);
            
            return new TokenValidationResult
            {
                AccessToken = encryptedAccessToken,
                RefreshToken = encryptedRefreshToken,
                ExpiresAt = expiresAt,
                WasRefreshed = false
            };
        }
        
        public async Task RevokeTokenAsync(string encryptedAccessToken)
        {
            _logger.LogDebug("Revoking Strava access token");
            
            try
            {
                var accessToken = _encryptionService.Decrypt(encryptedAccessToken);
                
                var request = new HttpRequestMessage(HttpMethod.Post, "https://www.strava.com/oauth/deauthorize");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                
                var response = await _httpClient.SendAsync(request);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Token revocation returned non-OK status: {Status}", response.StatusCode);
                }
                else
                {
                    _logger.LogInformation("Access token revoked successfully");
                }
            }
            catch (Exception ex)
            {
                // Don't throw - revocation failure shouldn't prevent logout
                _logger.LogWarning(ex, "Failed to revoke access token");
            }
        }
        
        private async Task HandleApiError(HttpResponseMessage response, string operation, object context)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            var statusCode = (int)response.StatusCode;
            
            var errorMessage = statusCode switch
            {
                401 => "Strava access token expired or invalid",
                403 => "Not authorized to perform this action",
                404 => "Resource not found or not accessible",
                429 => "Rate limit exceeded",
                _ => $"Strava API error ({statusCode}): {errorContent}"
            };
            
            _logger.LogError("{Operation} failed: {Error}. Context: {Context}", 
                operation, errorMessage, JsonSerializer.Serialize(context));
            
            throw new InvalidOperationException(errorMessage);
        }
    }
}