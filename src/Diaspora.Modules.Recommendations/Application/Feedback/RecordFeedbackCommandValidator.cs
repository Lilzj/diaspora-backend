using Diaspora.Modules.Recommendations.Domain.Enums;
using FluentValidation;

namespace Diaspora.Modules.Recommendations.Application.Feedback;

/// <summary>What a client may report.</summary>
public sealed class RecordFeedbackCommandValidator : AbstractValidator<RecordFeedbackCommand>
{
    public RecordFeedbackCommandValidator()
    {
        RuleFor(command => command.Kind)
            .NotEmpty()
            .Must(kind => Enum.TryParse<RecommendationFeedbackKind>(kind, ignoreCase: true, out _))
            .WithMessage("'{PropertyValue}' is not a feedback kind.");

        RuleFor(command => command.TargetType)
            .NotEmpty()
            .Must(type => Enum.TryParse<RecommendationTargetType>(type, ignoreCase: true, out _))
            .WithMessage("'{PropertyValue}' is not a recommendation target type.");

        RuleFor(command => command.Surface)
            .NotEmpty()
            .Must(surface => Enum.TryParse<RecommendationSurface>(surface, ignoreCase: true, out _))
            .WithMessage("'{PropertyValue}' is not a recommendation surface.");

        RuleFor(command => command.TargetId)
            .NotEmpty()
            .WithMessage("A target identifier is required.");

        RuleFor(command => command.Position)
            .GreaterThanOrEqualTo(0)
            .When(command => command.Position is not null);
    }
}
