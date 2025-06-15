using System.Diagnostics;

namespace StravaWeather.Api.Middleware
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;
        
        public RequestLoggingMiddleware(
            RequestDelegate next,
            ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }
        
        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var requestId = context.TraceIdentifier;
            
            // Add request ID to logging context
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["RequestId"] = requestId,
                ["RequestPath"] = context.Request.Path,
                ["RequestMethod"] = context.Request.Method
            }))
            {
                // Log request
                if (ShouldLogRequest(context.Request))
                {
                    _logger.LogInformation(
                        "HTTP {Method} {Path} started",
                        context.Request.Method,
                        context.Request.Path);
                }
                
                try
                {
                    await _next(context);
                }
                finally
                {
                    stopwatch.Stop();
                    
                    // Log response
                    if (ShouldLogResponse(context.Request, context.Response))
                    {
                        var logLevel = GetLogLevel(context.Response.StatusCode);
                        _logger.Log(
                            logLevel,
                            "HTTP {Method} {Path} responded {StatusCode} in {Elapsed:0.0000} ms",
                            context.Request.Method,
                            context.Request.Path,
                            context.Response.StatusCode,
                            stopwatch.Elapsed.TotalMilliseconds);
                    }
                }
            }
        }
        
        private static bool ShouldLogRequest(HttpRequest request)
        {
            // Skip health check logs in production
            if (request.Path.StartsWithSegments("/api/health"))
                return false;
                
            // Always log webhook requests
            if (request.Path.StartsWithSegments("/api/strava/webhook"))
                return true;
                
            // Log API requests
            return request.Path.StartsWithSegments("/api");
        }
        
        private static bool ShouldLogResponse(HttpRequest request, HttpResponse response)
        {
            // Always log errors
            if (response.StatusCode >= 400)
                return true;
                
            // Always log webhook responses
            if (request.Path.StartsWithSegments("/api/strava/webhook"))
                return true;
                
            // Skip health check responses in production
            if (request.Path.StartsWithSegments("/api/health"))
                return false;
                
            return request.Path.StartsWithSegments("/api");
        }
        
        private static LogLevel GetLogLevel(int statusCode)
        {
            return statusCode switch
            {
                >= 500 => LogLevel.Error,
                >= 400 => LogLevel.Warning,
                _ => LogLevel.Information
            };
        }
    }
}