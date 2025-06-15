using System.Net;
using System.Text.Json;
using FluentValidation;

namespace StravaWeather.Api.Middleware
{
    public class ErrorHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ErrorHandlingMiddleware> _logger;
        private readonly IWebHostEnvironment _environment;
        
        public ErrorHandlingMiddleware(
            RequestDelegate next,
            ILogger<ErrorHandlingMiddleware> logger,
            IWebHostEnvironment environment)
        {
            _next = next;
            _logger = logger;
            _environment = environment;
        }
        
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred");
                await HandleExceptionAsync(context, ex);
            }
        }
        
        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";
            
            var (statusCode, message) = exception switch
            {
                UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Unauthorized"),
                KeyNotFoundException => (HttpStatusCode.NotFound, "Resource not found"),
                ArgumentException => (HttpStatusCode.BadRequest, exception.Message),
                InvalidOperationException => (HttpStatusCode.BadRequest, exception.Message),
                ValidationException validationEx => (HttpStatusCode.BadRequest, GetValidationMessage(validationEx)),
                _ => (HttpStatusCode.InternalServerError, "An error occurred while processing your request")
            };
            
            context.Response.StatusCode = (int)statusCode;
            
            var response = new
            {
                error = new
                {
                    message,
                    statusCode = (int)statusCode,
                    timestamp = DateTime.UtcNow.ToString("O"),
                    requestId = context.TraceIdentifier,
                    stack = _environment.IsDevelopment() ? exception.StackTrace : null,
                    details = _environment.IsDevelopment() && exception.InnerException != null 
                        ? exception.InnerException.Message : null
                }
            };
            
            var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            await context.Response.WriteAsync(jsonResponse);
        }
        
        private static string GetValidationMessage(ValidationException ex)
        {
            var errors = ex.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}");
            return $"Validation failed: {string.Join("; ", errors)}";
        }
    }
}