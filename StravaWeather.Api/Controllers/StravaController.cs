using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StravaWeather.Api.Data;
using StravaWeather.Api.Models.DTOs;
using StravaWeather.Api.Services.Interfaces;
using StravaWeather.Api.Utils;

namespace StravaWeather.Api.Controllers
{
    [ApiController]
    [Route("api/strava")]
    public class StravaController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IActivityProcessor _activityProcessor;
        private readonly IConfiguration _configuration;
        private readonly ILogger<StravaController> _logger;
        
        public StravaController(
            ApplicationDbContext dbContext,
            IActivityProcessor activityProcessor,
            IConfiguration configuration,
            ILogger<StravaController> logger)
        {
            _dbContext = dbContext;
            _activityProcessor = activityProcessor;
            _configuration = configuration;
            _logger = logger;
        }
        
        // GET: /api/strava/webhook
        [HttpGet("webhook")]
        public IActionResult VerifyWebhook(
            [FromQuery(Name = "hub.mode")] string? mode,
            [FromQuery(Name = "hub.verify_token")] string? token,
            [FromQuery(Name = "hub.challenge")] string? challenge)
        {
            _logger.LogInformation("Webhook verification request received");
            
            if (mode == "subscribe" && token == _configuration["STRAVA_WEBHOOK_VERIFY_TOKEN"])
            {
                _logger.LogInformation("Webhook verification successful");
                return Ok(new Dictionary<string, string> { ["hub.challenge"] = challenge! });
            }
            
            _logger.LogWarning("Webhook verification failed");
            return StatusCode(403, new { error = "Verification failed" });
        }
        
        // POST: /api/strava/webhook
        [HttpPost("webhook")]
        public async Task<IActionResult> HandleWebhook([FromBody] StravaWebhookEventDto webhookEvent)
        {
            var requestId = HttpContext.TraceIdentifier;
            
            try
            {
                _logger.LogInformation(
                    "Webhook event received: {EventType} for {ObjectType} {ObjectId}",
                    webhookEvent.AspectType, webhookEvent.ObjectType, webhookEvent.ObjectId);
                
                // Only process new activity creations
                if (webhookEvent.ObjectType != "activity" || webhookEvent.AspectType != "create")
                {
                    return Ok(new { message = "Event acknowledged" });
                }
                
                var activityId = webhookEvent.ObjectId.ToString();
                var stravaAthleteId = webhookEvent.OwnerId.ToString();
                
                // Check if user exists and has weather enabled
                var user = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.StravaAthleteId == stravaAthleteId);
                    
                if (user == null || !user.WeatherEnabled)
                {
                    _logger.LogInformation("Skipping activity for user {AthleteId}", stravaAthleteId);
                    return Ok(new { message = "Event acknowledged" });
                }
                
                // Process with retry logic
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Constants.Webhook.MaxProcessingTimeMs / 1000));
                var result = await ProcessWithRetryAsync(activityId, user.Id, cts.Token);
                
                return Ok(new
                {
                    message = "Webhook processed",
                    activityId,
                    success = result.Success,
                    skipped = result.Skipped
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Webhook processing failed");
                // Always return 200 to prevent Strava retries
                return Ok(new { message = "Event acknowledged with error" });
            }
        }
        
        // GET: /api/strava/webhook/status
        [HttpGet("webhook/status")]
        public IActionResult GetWebhookStatus()
        {
            var status = new
            {
                configured = !string.IsNullOrEmpty(_configuration["STRAVA_WEBHOOK_VERIFY_TOKEN"]),
                endpoint = $"{_configuration["APP_URL"]}/api/strava/webhook",
                verifyTokenSet = !string.IsNullOrEmpty(_configuration["STRAVA_WEBHOOK_VERIFY_TOKEN"]),
                timestamp = DateTime.UtcNow
            };
            
            return Ok(ApiResponse<object>.SuccessResponse(status, "Webhook endpoint is active"));
        }
        
        private async Task<ProcessingResult> ProcessWithRetryAsync(
            string activityId,
            Guid userId,
            CancellationToken cancellationToken)
        {
            var attempts = 0;
            var errors = new List<(int attempt, string error)>();
            
            while (attempts < Constants.Webhook.MaxRetryAttempts && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (attempts > 0)
                    {
                        var delayIndex = Math.Min(attempts - 1, Constants.Webhook.RetryDelaysMs.Length - 1);
                        var delay = Constants.Webhook.RetryDelaysMs[delayIndex];
                        
                        _logger.LogInformation("Retrying activity processing. Attempt: {Attempt}, Delay: {Delay}ms",
                            attempts + 1, delay);
                            
                        await Task.Delay(delay, cancellationToken);
                    }
                    
                    var result = await _activityProcessor.ProcessActivityAsync(activityId, userId);
                    
                    if (result.Success || result.Skipped || !IsRetryableError(result.Error))
                    {
                        return result;
                    }
                    
                    errors.Add((attempts + 1, result.Error ?? "Unknown error"));
                    attempts++;
                }
                catch (Exception ex)
                {
                    errors.Add((attempts + 1, ex.Message));
                    _logger.LogError(ex, "Activity processing attempt {Attempt} failed", attempts + 1);
                    attempts++;
                }
            }
            
            return new ProcessingResult
            {
                Success = false,
                ActivityId = activityId,
                Error = $"Max retry attempts exceeded. Errors: {string.Join("; ", errors.Select(e => $"Attempt {e.attempt}: {e.error}"))}"
            };
        }
        
        private bool IsRetryableError(string? error)
        {
            if (string.IsNullOrEmpty(error)) return false;
            
            var errorLower = error.ToLowerInvariant();
            return errorLower.Contains("not found") || errorLower.Contains("404");
        }
    }
}