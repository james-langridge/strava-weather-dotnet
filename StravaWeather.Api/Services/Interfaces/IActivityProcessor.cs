using StravaWeather.Api.Models.DTOs;

namespace StravaWeather.Api.Services.Interfaces
{
    public interface IActivityProcessor
    {
        Task<ProcessingResult> ProcessActivityAsync(string activityId, Guid userId);
    }
}