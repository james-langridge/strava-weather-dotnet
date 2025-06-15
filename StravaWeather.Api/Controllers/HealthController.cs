using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StravaWeather.Api.Data;

namespace StravaWeather.Api.Controllers
{
    [ApiController]
    [Route("api/health")]
    public class HealthController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IConfiguration _configuration;
        private readonly ILogger<HealthController> _logger;
        
        public HealthController(
            ApplicationDbContext dbContext,
            IConfiguration configuration,
            ILogger<HealthController> logger)
        {
            _dbContext = dbContext;
            _configuration = configuration;
            _logger = logger;
        }
        
        // GET: /api/health
        [HttpGet]
        public async Task<IActionResult> GetHealth()
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                await _dbContext.Database.ExecuteSqlRawAsync("SELECT 1");
                
                var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                
                return Ok(new
                {
                    status = "healthy",
                    timestamp = DateTime.UtcNow.ToString("O"),
                    responseTime = $"{responseTime:F0}ms",
                    environment = _configuration["ASPNETCORE_ENVIRONMENT"]
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed");
                
                var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                
                Response.StatusCode = 503;
                return new ObjectResult(new
                {
                    status = "unhealthy",
                    timestamp = DateTime.UtcNow.ToString("O"),
                    responseTime = $"{responseTime:F0}ms",
                    environment = _configuration["ASPNETCORE_ENVIRONMENT"],
                    error = ex.Message
                })
                {
                    StatusCode = 503
                };
            }
        }
        
        // GET: /api/health/detailed
        [HttpGet("detailed")]
        public async Task<IActionResult> GetDetailedHealth()
        {
            var healthStatus = new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow.ToString("O"),
                uptime = GetUptime(),
                version = "1.0.0",
                environment = _configuration["ASPNETCORE_ENVIRONMENT"],
                services = new
                {
                    database = await CheckDatabaseAsync(),
                    stravaApi = await CheckStravaApiAsync(),
                    weatherApi = await CheckWeatherApiAsync()
                },
                performance = new
                {
                    memory = GC.GetTotalMemory(false),
                    gen0Collections = GC.CollectionCount(0),
                    gen1Collections = GC.CollectionCount(1),
                    gen2Collections = GC.CollectionCount(2)
                }
            };
            
            // Determine overall status
            var services = new[] { healthStatus.services.database, healthStatus.services.stravaApi, healthStatus.services.weatherApi };
            var unhealthyCount = services.Count(s => s.status == "unhealthy");
            var status = unhealthyCount > 0 ? "unhealthy" : "healthy";
            
            var statusCode = status == "healthy" ? 200 : 503;
            
            Response.StatusCode = statusCode;
            return new ObjectResult(healthStatus) { StatusCode = statusCode };
        }
        
        // GET: /api/health/ready
        [HttpGet("ready")]
        public async Task<IActionResult> GetReadiness()
        {
            try
            {
                await _dbContext.Database.ExecuteSqlRawAsync("SELECT 1");
                
                return Ok(new
                {
                    status = "ready",
                    timestamp = DateTime.UtcNow.ToString("O")
                });
            }
            catch
            {
                Response.StatusCode = 503;
                return new ObjectResult(new
                {
                    status = "not_ready",
                    timestamp = DateTime.UtcNow.ToString("O"),
                    error = "Critical dependency failure"
                })
                {
                    StatusCode = 503
                };
            }
        }
        
        // GET: /api/health/live
        [HttpGet("live")]
        public IActionResult GetLiveness()
        {
            return Ok(new
            {
                status = "alive",
                timestamp = DateTime.UtcNow.ToString("O"),
                uptime = GetUptime()
            });
        }
        
        private string GetUptime()
        {
            var uptime = DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime;
            return $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s";
        }
        
        private async Task<object> CheckDatabaseAsync()
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                await _dbContext.Database.ExecuteSqlRawAsync("SELECT 1");
                
                return new
                {
                    status = "healthy",
                    responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds,
                    lastChecked = DateTime.UtcNow.ToString("O")
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    status = "unhealthy",
                    responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds,
                    error = ex.Message,
                    lastChecked = DateTime.UtcNow.ToString("O")
                };
            }
        }
        
        private async Task<object> CheckStravaApiAsync()
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                using var httpClient = new HttpClient();
                var response = await httpClient.GetAsync("https://www.strava.com/api/v3/athlete");
                
                var isAccessible = response.StatusCode == System.Net.HttpStatusCode.Unauthorized || 
                                  response.StatusCode == System.Net.HttpStatusCode.OK;
                
                return new
                {
                    status = isAccessible ? "healthy" : "unhealthy",
                    responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds,
                    lastChecked = DateTime.UtcNow.ToString("O"),
                    error = !isAccessible ? $"Unexpected status: {response.StatusCode}" : null
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    status = "unhealthy",
                    responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds,
                    error = ex.Message,
                    lastChecked = DateTime.UtcNow.ToString("O")
                };
            }
        }
        
        private async Task<object> CheckWeatherApiAsync()
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                using var httpClient = new HttpClient();
                var testUrl = $"https://api.openweathermap.org/data/3.0/onecall?" +
                             $"lat=0&lon=0&appid={_configuration["OPENWEATHERMAP_API_KEY"]}&exclude=all";
                
                var response = await httpClient.GetAsync(testUrl);
                
                var isAccessible = response.StatusCode == System.Net.HttpStatusCode.OK || 
                                  response.StatusCode == System.Net.HttpStatusCode.BadRequest;
                
                return new
                {
                    status = isAccessible ? "healthy" : "unhealthy",
                    responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds,
                    lastChecked = DateTime.UtcNow.ToString("O"),
                    error = !isAccessible ? $"Unexpected status: {response.StatusCode}" : null
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    status = "unhealthy",
                    responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds,
                    error = ex.Message,
                    lastChecked = DateTime.UtcNow.ToString("O")
                };
            }
        }
    }
}