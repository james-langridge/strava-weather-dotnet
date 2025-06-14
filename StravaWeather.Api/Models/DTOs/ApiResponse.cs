namespace StravaWeather.Api.Models.DTOs
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? Message { get; set; }
        public string? Error { get; set; }
        
        public static ApiResponse<T> SuccessResponse(T data, string? message = null)
        {
            return new ApiResponse<T>
            {
                Success = true,
                Data = data,
                Message = message
            };
        }
        
        public static ApiResponse<T> ErrorResponse(string error, string? message = null)
        {
            return new ApiResponse<T>
            {
                Success = false,
                Error = error,
                Message = message
            };
        }
    }
}