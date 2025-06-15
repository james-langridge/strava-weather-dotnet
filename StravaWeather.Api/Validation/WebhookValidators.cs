using FluentValidation;
using StravaWeather.Api.Models.DTOs;

namespace StravaWeather.Api.Validation
{
    public class StravaWebhookEventDtoValidator : AbstractValidator<StravaWebhookEventDto>
    {
        public StravaWebhookEventDtoValidator()
        {
            RuleFor(x => x.ObjectType)
                .NotEmpty()
                .Must(x => x == "activity" || x == "athlete")
                .WithMessage("Object type must be 'activity' or 'athlete'");
                
            RuleFor(x => x.AspectType)
                .NotEmpty()
                .Must(x => x == "create" || x == "update" || x == "delete")
                .WithMessage("Aspect type must be 'create', 'update', or 'delete'");
                
            RuleFor(x => x.ObjectId)
                .GreaterThan(0)
                .WithMessage("Object ID must be positive");
                
            RuleFor(x => x.OwnerId)
                .GreaterThan(0)
                .WithMessage("Owner ID must be positive");
                
            RuleFor(x => x.SubscriptionId)
                .GreaterThan(0)
                .WithMessage("Subscription ID must be positive");
        }
    }
}