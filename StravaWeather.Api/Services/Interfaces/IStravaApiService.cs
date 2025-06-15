using StravaWeather.Api.Models.DTOs;

namespace StravaWeather.Api.Services.Interfaces
{
    public interface IStravaApiService
    {
        Task<StravaActivity?> GetActivityAsync(string activityId, string encryptedAccessToken);
        Task<StravaActivity> UpdateActivityAsync(string activityId, string encryptedAccessToken, StravaUpdateData updateData);
        Task<TokenRefreshResult> RefreshAccessTokenAsync(string encryptedRefreshToken);
        Task<TokenValidationResult> EnsureValidTokenAsync(string encryptedAccessToken, string encryptedRefreshToken, DateTime expiresAt);
        Task RevokeTokenAsync(string encryptedAccessToken);
    }
}