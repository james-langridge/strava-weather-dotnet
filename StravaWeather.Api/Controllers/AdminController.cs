using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StravaWeather.Api.Data;
using StravaWeather.Api.Models.DTOs;
using StravaWeather.Api.Services.Interfaces;

namespace StravaWeather.Api.Controllers
{
    [ApiController]
    [Route("api/admin")]
    public class AdminController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IWebhookSubscriptionService _webhookService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AdminController> _logger;
        
        public AdminController(
            ApplicationDbContext dbContext,
            IWebhookSubscriptionService webhookService,
            IConfiguration configuration,
            ILogger<AdminController> logger)
        {
            _dbContext = dbContext;
            _webhookService = webhookService;
            _configuration = configuration;
            _logger = logger;
        }
        
        // GET: /api/admin/webhook/status
        [HttpGet("webhook/status")]
        [RequireAdminAuth]
        public async Task<IActionResult> GetWebhookStatus()
        {
            var subscription = await _webhookService.ViewSubscriptionAsync();
            
            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                hasSubscription = subscription != null,
                subscription,
                webhookEndpoint = $"{_configuration["APP_URL"]}/api/strava/webhook",
                verifyToken = !string.IsNullOrEmpty(_configuration["STRAVA_WEBHOOK_VERIFY_TOKEN"]) 
                    ? "configured" : "missing"
            }));
        }
        
        // POST: /api/admin/webhook/subscribe
        [HttpPost("webhook/subscribe")]
        [RequireAdminAuth]
        public async Task<IActionResult> CreateWebhookSubscription([FromBody] CreateSubscriptionDto dto)
        {
            var callbackUrl = dto.CallbackUrl ?? $"{_configuration["APP_URL"]}/api/strava/webhook";
            
            _logger.LogInformation("Creating webhook subscription with callback URL: {CallbackUrl}", callbackUrl);
            
            // Verify endpoint is accessible
            var isAccessible = await _webhookService.VerifyEndpointAsync(callbackUrl);
            
            if (!isAccessible)
            {
                return BadRequest(ApiResponse<object>.ErrorResponse(
                    "Webhook endpoint is not accessible. Ensure your server is publicly accessible."));
            }
            
            // Create subscription
            var subscription = await _webhookService.CreateSubscriptionAsync(callbackUrl);
            
            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                subscription,
                callbackUrl
            }, "Webhook subscription created successfully"));
        }
        
        // DELETE: /api/admin/webhook/unsubscribe
        [HttpDelete("webhook/unsubscribe")]
        [RequireAdminAuth]
        public async Task<IActionResult> DeleteWebhookSubscription()
        {
            var subscription = await _webhookService.ViewSubscriptionAsync();
            
            if (subscription == null)
            {
                return NotFound(ApiResponse<object>.ErrorResponse("No webhook subscription found"));
            }
            
            await _webhookService.DeleteSubscriptionAsync(subscription.Id);
            
            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                deletedSubscriptionId = subscription.Id
            }, "Webhook subscription deleted successfully"));
        }
        
        // GET: /api/admin/webhook/verify
        [HttpGet("webhook/verify")]
        [RequireAdminAuth]
        public async Task<IActionResult> VerifyWebhookEndpoint()
        {
            var callbackUrl = $"{_configuration["APP_URL"]}/api/strava/webhook";
            var isAccessible = await _webhookService.VerifyEndpointAsync(callbackUrl);
            
            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                callbackUrl,
                verified = isAccessible
            }, isAccessible 
                ? "Webhook endpoint is accessible and working correctly"
                : "Webhook endpoint verification failed"));
        }
        
        // POST: /api/admin/webhook/setup
        [HttpPost("webhook/setup")]
        [RequireAdminAuth]
        public async Task<IActionResult> SetupWebhook([FromBody] SetupWebhookDto dto)
        {
            _logger.LogInformation("Starting webhook setup process");
            
            // Check for existing subscription
            var existing = await _webhookService.ViewSubscriptionAsync();
            
            if (existing != null)
            {
                return Ok(ApiResponse<object>.SuccessResponse(new
                {
                    subscription = existing,
                    action = "existing"
                }, "Webhook subscription already exists"));
            }
            
            var callbackUrl = dto.BaseUrl != null 
                ? $"{dto.BaseUrl}/api/strava/webhook"
                : $"{_configuration["APP_URL"]}/api/strava/webhook";
                
            _logger.LogInformation("Setting up webhook with callback URL: {CallbackUrl}", callbackUrl);
            
            // Verify endpoint
            var isAccessible = await _webhookService.VerifyEndpointAsync(callbackUrl);
            
            if (!isAccessible)
            {
                return BadRequest(ApiResponse<object>.ErrorResponse(
                    $"Webhook endpoint is not accessible at {callbackUrl}. " +
                    "Ensure your server is publicly accessible."));
            }
            
            // Create subscription
            var subscription = await _webhookService.CreateSubscriptionAsync(callbackUrl);
            
            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                subscription,
                callbackUrl,
                action = "created"
            }, "Webhook subscription created successfully"));
        }
        
        // GET: /api/admin/webhook/monitor
        [HttpGet("webhook/monitor")]
        [RequireAdminAuth]
        public async Task<IActionResult> MonitorWebhooks()
        {
            var subscription = await _webhookService.ViewSubscriptionAsync();
            
            // Get recent user activities
            var recentUsers = await _dbContext.Users
                .Where(u => u.UpdatedAt >= DateTime.UtcNow.AddDays(-1))
                .OrderByDescending(u => u.UpdatedAt)
                .Take(10)
                .Select(u => new
                {
                    name = $"{u.FirstName} {u.LastName}",
                    stravaId = u.StravaAthleteId,
                    weatherEnabled = u.WeatherEnabled,
                    lastActive = u.UpdatedAt,
                    tokenValid = u.TokenExpiresAt > DateTime.UtcNow
                })
                .ToListAsync();
                
            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                webhook = new
                {
                    hasSubscription = subscription != null,
                    subscriptionId = subscription?.Id,
                    callbackUrl = subscription?.CallbackUrl,
                    createdAt = subscription?.CreatedAt
                },
                recentActivity = new
                {
                    usersUpdatedLast24h = recentUsers.Count,
                    users = recentUsers
                },
                environment = new
                {
                    nodeEnv = _configuration["ASPNETCORE_ENVIRONMENT"],
                    hasAppUrl = !string.IsNullOrEmpty(_configuration["APP_URL"]),
                    appUrl = _configuration["APP_URL"]
                }
            }));
        }
        
        // DTOs
        public class CreateSubscriptionDto
        {
            public string? CallbackUrl { get; set; }
        }
        
        public class SetupWebhookDto
        {
            public string? BaseUrl { get; set; }
        }
    }
    
    // Admin authentication attribute
    public class RequireAdminAuthAttribute : TypeFilterAttribute
    {
        public RequireAdminAuthAttribute() : base(typeof(AdminAuthFilter)) { }
    }
    
    public class AdminAuthFilter : IAuthorizationFilter
    {
        private readonly IConfiguration _configuration;
        
        public AdminAuthFilter(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var adminToken = context.HttpContext.Request.Headers["x-admin-token"].FirstOrDefault() 
                          ?? context.HttpContext.Request.Query["admin_token"].FirstOrDefault();
                          
            var expectedToken = _configuration["ADMIN_TOKEN"] ?? "your-secret-admin-token";
            
            if (adminToken != expectedToken)
            {
                context.Result = new ObjectResult(ApiResponse<object>.ErrorResponse(
                    "Unauthorized - Admin access required"))
                {
                    StatusCode = 401
                };
            }
        }
    }
}