using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StravaWeather.Api.Data;
using StravaWeather.Api.Models.DTOs;
using StravaWeather.Api.Services.Interfaces;
using StravaWeather.Api.Utils;

namespace StravaWeather.Api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IStravaApiService _stravaApiService;
        private readonly IEncryptionService _encryptionService;
        private readonly IWebhookSubscriptionService _webhookService;
        private readonly JwtHelper _jwtHelper;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;
        
        private readonly string[] StravaScopes = { "activity:read_all", "activity:write", "profile:read_all" };
        
        public AuthController(
            ApplicationDbContext dbContext,
            IStravaApiService stravaApiService,
            IEncryptionService encryptionService,
            IWebhookSubscriptionService webhookService,
            JwtHelper jwtHelper,
            IConfiguration configuration,
            ILogger<AuthController> logger)
        {
            _dbContext = dbContext;
            _stravaApiService = stravaApiService;
            _encryptionService = encryptionService;
            _webhookService = webhookService;
            _jwtHelper = jwtHelper;
            _configuration = configuration;
            _logger = logger;
        }
        
        // GET: /api/auth/strava
        [HttpGet("strava")]
        public IActionResult InitiateStravaAuth()
        {
            var state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            
            _logger.LogInformation("Initiating Strava OAuth flow");
            
            // TODO: Store state in distributed cache for verification
            
            var authUrl = new UriBuilder("https://www.strava.com/oauth/authorize");
            var query = System.Web.HttpUtility.ParseQueryString(string.Empty);
            query["client_id"] = _configuration["STRAVA_CLIENT_ID"];
            query["redirect_uri"] = $"{_configuration["APP_URL"]}/api/auth/strava/callback";
            query["response_type"] = "code";
            query["approval_prompt"] = "force";
            query["scope"] = string.Join(",", StravaScopes);
            query["state"] = state;
            authUrl.Query = query.ToString();
            
            return Redirect(authUrl.ToString());
        }
        
        // GET: /api/auth/strava/callback
        [HttpGet("strava/callback")]
        public async Task<IActionResult> HandleStravaCallback(
            [FromQuery] string? code,
            [FromQuery] string? state,
            [FromQuery] string? error)
        {
            var requestId = HttpContext.TraceIdentifier;
            
            _logger.LogInformation("OAuth callback received. RequestId: {RequestId}", requestId);
            
            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogWarning("OAuth authorization denied: {Error}", error);
                return Redirect($"{_configuration["APP_URL"]}/auth/error?error={Uri.EscapeDataString("Authorization denied")}");
            }
            
            if (string.IsNullOrEmpty(code))
            {
                _logger.LogWarning("OAuth callback missing authorization code");
                return Redirect($"{_configuration["APP_URL"]}/auth/error?error={Uri.EscapeDataString("Authorization code not received")}");
            }
            
            // TODO: Verify state parameter
            
            try
            {
                // Exchange code for tokens
                var tokenResponse = await ExchangeCodeForTokenAsync(code);
                
                if (tokenResponse?.Athlete == null)
                {
                    _logger.LogError("Token response missing athlete data");
                    return Redirect($"{_configuration["APP_URL"]}/auth/error?error={Uri.EscapeDataString("Unable to retrieve athlete data")}");
                }
                
                // Encrypt tokens
                var encryptedAccessToken = _encryptionService.Encrypt(tokenResponse.AccessToken);
                var encryptedRefreshToken = _encryptionService.Encrypt(tokenResponse.RefreshToken);
                
                // Upsert user
                var user = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.StravaAthleteId == tokenResponse.Athlete.Id.ToString());
                
                var isNewUser = user == null;
                
                if (user == null)
                {
                    user = new Models.Entities.User
                    {
                        StravaAthleteId = tokenResponse.Athlete.Id.ToString()
                    };
                    _dbContext.Users.Add(user);
                }
                
                // Update user data
                user.AccessToken = encryptedAccessToken;
                user.RefreshToken = encryptedRefreshToken;
                user.TokenExpiresAt = DateTimeOffset.FromUnixTimeSeconds(tokenResponse.ExpiresAt).UtcDateTime;
                user.FirstName = tokenResponse.Athlete.FirstName ?? "";
                user.LastName = tokenResponse.Athlete.LastName ?? "";
                user.ProfileImageUrl = tokenResponse.Athlete.ProfileMedium ?? tokenResponse.Athlete.Profile;
                user.City = tokenResponse.Athlete.City;
                user.State = tokenResponse.Athlete.State;
                user.Country = tokenResponse.Athlete.Country;
                user.WeatherEnabled = true;
                user.UpdatedAt = DateTime.UtcNow;
                
                await _dbContext.SaveChangesAsync();
                
                _logger.LogInformation("User {UserId} authenticated successfully", user.Id);
                
                // Ensure webhook subscription exists
                _ = Task.Run(async () => await _webhookService.EnsureSubscriptionExistsAsync());
                
                // Generate JWT and set cookie
                var token = _jwtHelper.GenerateToken(user.Id, user.StravaAthleteId);
                _jwtHelper.SetAuthCookie(Response, token);
                
                var redirectUrl = isNewUser 
                    ? $"{_configuration["APP_URL"]}/auth/success?new_user=true"
                    : $"{_configuration["APP_URL"]}/auth/success";
                    
                return Redirect(redirectUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OAuth flow failed");
                return Redirect($"{_configuration["APP_URL"]}/auth/error?error={Uri.EscapeDataString("Authentication failed")}");
            }
        }
        
        // POST: /api/auth/logout
        [HttpPost("logout")]
        public IActionResult Logout()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            
            _logger.LogInformation("User logout: {UserId}", userId);
            
            _jwtHelper.ClearAuthCookie(Response);
            
            return Ok(ApiResponse<object>.SuccessResponse(null, "Logged out successfully"));
        }
        
        // GET: /api/auth/check
        [HttpGet("check")]
        public async Task<IActionResult> CheckAuth()
        {
            var token = Request.Cookies[Constants.Jwt.CookieName];
            
            if (string.IsNullOrEmpty(token))
            {
                return Ok(ApiResponse<object>.SuccessResponse(new { authenticated = false }));
            }
            
            var principal = _jwtHelper.ValidateToken(token);
            if (principal == null)
            {
                _jwtHelper.ClearAuthCookie(Response);
                return Ok(ApiResponse<object>.SuccessResponse(new { authenticated = false }));
            }
            
            var userId = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
            {
                _jwtHelper.ClearAuthCookie(Response);
                return Ok(ApiResponse<object>.SuccessResponse(new { authenticated = false }));
            }
            
            var user = await _dbContext.Users.FindAsync(userGuid);
            if (user == null)
            {
                _jwtHelper.ClearAuthCookie(Response);
                return Ok(ApiResponse<object>.SuccessResponse(new { authenticated = false }));
            }
            
            return Ok(ApiResponse<object>.SuccessResponse(new 
            { 
                authenticated = true,
                user = new { id = user.Id, stravaAthleteId = user.StravaAthleteId }
            }));
        }
        
        // DELETE: /api/auth/revoke
        [HttpDelete("revoke")]
        [Authorize]
        public async Task<IActionResult> RevokeAccess()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userId, out var userGuid))
            {
                return Unauthorized();
            }
            
            var user = await _dbContext.Users.FindAsync(userGuid);
            if (user == null)
            {
                return NotFound();
            }
            
            _logger.LogInformation("Revoking Strava access for user {UserId}", userGuid);
            
            try
            {
                await _stravaApiService.RevokeTokenAsync(user.AccessToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to revoke Strava token");
            }
            
            _dbContext.Users.Remove(user);
            await _dbContext.SaveChangesAsync();
            
            _jwtHelper.ClearAuthCookie(Response);
            
            return Ok(ApiResponse<object>.SuccessResponse(null, 
                "Strava access revoked and account deleted successfully"));
        }
        
        private async Task<StravaTokenResponse?> ExchangeCodeForTokenAsync(string code)
        {
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", _configuration["STRAVA_CLIENT_ID"]!),
                new KeyValuePair<string, string>("client_secret", _configuration["STRAVA_CLIENT_SECRET"]!),
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("grant_type", "authorization_code")
            });
            
            using var httpClient = new HttpClient();
            var response = await httpClient.PostAsync("https://www.strava.com/oauth/token", formData);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Token exchange failed: {Status} - {Error}", response.StatusCode, error);
                throw new InvalidOperationException("Token exchange failed");
            }
            
            var json = await response.Content.ReadAsStringAsync();
            return System.Text.Json.JsonSerializer.Deserialize<StravaTokenResponse>(json);
        }
    }
}