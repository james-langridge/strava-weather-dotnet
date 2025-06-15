using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StravaWeather.Api.Models.DTOs;
using StravaWeather.Api.Services.Interfaces;

namespace StravaWeather.Api.Controllers
{
    [ApiController]
    [Route("api/activities")]
    [Authorize]
    public class ActivitiesController : ControllerBase
    {
        private readonly IActivityProcessor _activityProcessor;
        private readonly ILogger<ActivitiesController> _logger;
        
        public ActivitiesController(
            IActivityProcessor activityProcessor,
            ILogger<ActivitiesController> logger)
        {
            _activityProcessor = activityProcessor;
            _logger = logger;
        }
        
        // POST: /api/activities/process/{activityId}
        [HttpPost("process/{activityId}")]
        public async Task<IActionResult> ProcessActivity(string activityId)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(activityId, @"^\d+$"))
            {
                return BadRequest(ApiResponse<object>.ErrorResponse("Activity ID must be numeric"));
            }
            
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }
            
            _logger.LogInformation("Manual activity processing requested: {ActivityId} by {UserId}", 
                activityId, userId);
                
            var startTime = DateTime.UtcNow;
            var result = await _activityProcessor.ProcessActivityAsync(activityId, userId);
            var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
            
            if (result.Success)
            {
                _logger.LogInformation("Activity processing completed: {ActivityId}", activityId);
                
                var message = result.Skipped switch
                {
                    true when result.Reason == "Already has weather data" => 
                        "Activity already contains weather data",
                    true when result.Reason == "Weather updates disabled" => 
                        "Weather updates are currently disabled for your account",
                    true when result.Reason == "No GPS coordinates" => 
                        "Activity processed but no weather added (missing GPS data)",
                    true => $"Activity was skipped: {result.Reason}",
                    _ => "Activity processed successfully with weather data"
                };
                
                return Ok(ApiResponse<object>.SuccessResponse(new
                {
                    activityId = result.ActivityId,
                    weatherData = result.WeatherData,
                    skipped = result.Skipped,
                    reason = result.Reason,
                    processingTimeMs = processingTime
                }, message));
            }
            else
            {
                _logger.LogWarning("Activity processing failed: {ActivityId} - {Error}", 
                    activityId, result.Error);
                    
                var statusCode = GetErrorStatusCode(result.Error);
                
                Response.StatusCode = statusCode;
                return new ObjectResult(ApiResponse<object>.ErrorResponse(
                    result.Error ?? "Unknown error occurred",
                    "Failed to process activity"))
                {
                    StatusCode = statusCode
                };
            }
        }
        
        private int GetErrorStatusCode(string? error)
        {
            if (string.IsNullOrEmpty(error)) return 400;
            
            var errorLower = error.ToLowerInvariant();
            
            if (errorLower.Contains("not found") || errorLower.Contains("404"))
                return 404;
            if (errorLower.Contains("unauthorized") || errorLower.Contains("401"))
                return 401;
            if (errorLower.Contains("rate limit") || errorLower.Contains("429"))
                return 429;
            if (errorLower.Contains("unavailable") || errorLower.Contains("503"))
                return 503;
                
            return 400;
        }
    }
}