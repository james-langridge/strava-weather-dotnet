using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StravaWeather.Api.Data;
using StravaWeather.Api.Models.DTOs;
using StravaWeather.Api.Utils;

namespace StravaWeather.Api.Controllers
{
    [ApiController]
    [Route("api/users")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly JwtHelper _jwtHelper;
        private readonly ILogger<UsersController> _logger;
        
        public UsersController(
            ApplicationDbContext dbContext,
            JwtHelper jwtHelper,
            ILogger<UsersController> logger)
        {
            _dbContext = dbContext;
            _jwtHelper = jwtHelper;
            _logger = logger;
        }
        
        // GET: /api/users/me
        [HttpGet("me")]
        public async Task<IActionResult> GetCurrentUser()
        {
            var userId = GetUserId();
            if (!userId.HasValue) return Unauthorized();
            
            var user = await _dbContext.Users
                .Include(u => u.Preferences)
                .FirstOrDefaultAsync(u => u.Id == userId.Value);
                
            if (user == null)
            {
                _logger.LogError("User {UserId} not found in database", userId);
                return NotFound("User profile not found");
            }
            
            var locationParts = new[] { user.City, user.State, user.Country }
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
            
            var userDto = new UserDto
            {
                Id = user.Id,
                StravaAthleteId = user.StravaAthleteId,
                FirstName = user.FirstName ?? "",
                LastName = user.LastName ?? "",
                ProfileImageUrl = user.ProfileImageUrl,
                Location = locationParts.Any() ? string.Join(", ", locationParts) : null,
                WeatherEnabled = user.WeatherEnabled,
                MemberSince = user.CreatedAt,
                LastUpdated = user.UpdatedAt,
                Preferences = user.Preferences != null ? new UserPreferencesDto
                {
                    TemperatureUnit = user.Preferences.TemperatureUnit,
                    WeatherFormat = user.Preferences.WeatherFormat,
                    IncludeUvIndex = user.Preferences.IncludeUvIndex,
                    IncludeVisibility = user.Preferences.IncludeVisibility,
                    CustomFormat = user.Preferences.CustomFormat,
                    UpdatedAt = user.Preferences.UpdatedAt
                } : new UserPreferencesDto()
            };
            
            return Ok(ApiResponse<UserDto>.SuccessResponse(userDto));
        }
        
        // PATCH: /api/users/me
        [HttpPatch("me")]
        public async Task<IActionResult> UpdateUser([FromBody] UpdateUserDto updateDto)
        {
            var userId = GetUserId();
            if (!userId.HasValue) return Unauthorized();
            
            var user = await _dbContext.Users.FindAsync(userId.Value);
            if (user == null) return NotFound();
            
            if (updateDto.WeatherEnabled.HasValue)
            {
                user.WeatherEnabled = updateDto.WeatherEnabled.Value;
                user.UpdatedAt = DateTime.UtcNow;
            }
            
            await _dbContext.SaveChangesAsync();
            
            _logger.LogInformation("User {UserId} settings updated", userId);
            
            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                id = user.Id,
                weatherEnabled = user.WeatherEnabled,
                updatedAt = user.UpdatedAt
            }, "User settings updated successfully"));
        }
        
        // PATCH: /api/users/me/preferences
        [HttpPatch("me/preferences")]
        public async Task<IActionResult> UpdatePreferences([FromBody] UpdatePreferencesDto updateDto)
        {
            var userId = GetUserId();
            if (!userId.HasValue) return Unauthorized();
            
            var preferences = await _dbContext.UserPreferences
                .FirstOrDefaultAsync(p => p.UserId == userId.Value);
                
            if (preferences == null)
            {
                preferences = new Models.Entities.UserPreference
                {
                    UserId = userId.Value
                };
                _dbContext.UserPreferences.Add(preferences);
            }
            
            if (!string.IsNullOrEmpty(updateDto.TemperatureUnit))
                preferences.TemperatureUnit = updateDto.TemperatureUnit;
            if (!string.IsNullOrEmpty(updateDto.WeatherFormat))
                preferences.WeatherFormat = updateDto.WeatherFormat;
            if (updateDto.IncludeUvIndex.HasValue)
                preferences.IncludeUvIndex = updateDto.IncludeUvIndex.Value;
            if (updateDto.IncludeVisibility.HasValue)
                preferences.IncludeVisibility = updateDto.IncludeVisibility.Value;
            if (updateDto.CustomFormat != null)
                preferences.CustomFormat = updateDto.CustomFormat;
                
            preferences.UpdatedAt = DateTime.UtcNow;
            
            await _dbContext.SaveChangesAsync();
            
            _logger.LogInformation("User {UserId} preferences updated", userId);
            
            var preferencesDto = new UserPreferencesDto
            {
                TemperatureUnit = preferences.TemperatureUnit,
                WeatherFormat = preferences.WeatherFormat,
                IncludeUvIndex = preferences.IncludeUvIndex,
                IncludeVisibility = preferences.IncludeVisibility,
                CustomFormat = preferences.CustomFormat,
                UpdatedAt = preferences.UpdatedAt
            };
            
            return Ok(ApiResponse<UserPreferencesDto>.SuccessResponse(preferencesDto, 
                "Weather preferences updated successfully"));
        }
        
        // DELETE: /api/users/me
        [HttpDelete("me")]
        public async Task<IActionResult> DeleteAccount()
        {
            var userId = GetUserId();
            if (!userId.HasValue) return Unauthorized();
            
            var user = await _dbContext.Users.FindAsync(userId.Value);
            if (user == null) return NotFound();
            
            _logger.LogWarning("User account deletion requested: {UserId}", userId);
            
            _dbContext.Users.Remove(user);
            await _dbContext.SaveChangesAsync();
            
            _jwtHelper.ClearAuthCookie(Response);
            
            return Ok(ApiResponse<object>.SuccessResponse(null, 
                "Your account has been deleted successfully"));
        }
        
        private Guid? GetUserId()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        }
    }
}