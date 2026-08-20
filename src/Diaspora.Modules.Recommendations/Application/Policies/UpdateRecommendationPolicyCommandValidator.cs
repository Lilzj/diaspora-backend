using Diaspora.Modules.Recommendations.Domain.Enums;
using FluentValidation;

namespace Diaspora.Modules.Recommendations.Application.Policies;

/// <summary>
/// What an operator may set on a policy.
/// <para>
/// <b>Nothing here refuses disabling a system policy.</b> That rule lives in the aggregate, because a
/// validator only guards the path it is attached to and a domain rule guards every path. The
/// administration response marks which policies are immutable so the interface does not offer the
/// action.
/// </para>
/// </summary>
public sealed class UpdateRecommendationPolicyCommandValidator
    : AbstractValidator<UpdateRecommendationPolicyCommand>
{
    public UpdateRecommendationPolicyCommandValidator()
    {
        RuleFor(command => command.PolicyId).NotEmpty();

        RuleFor(command => command.DemotionFactor)
            .InclusiveBetween(0.01m, 1m)
            .When(command => command.DemotionFactor is not null)
            .WithMessage(
                "A demotion factor is above zero and at most one. Zero would be an exclusion wearing "
                + "a demotion's clothes.");

        RuleFor(command => command.Description).MaximumLength(512);

        RuleForEach(command => command.Surfaces!)
            .Must(surface => Enum.TryParse<RecommendationSurface>(surface, ignoreCase: true, out _))
            .When(command => command.Surfaces is not null)
            .WithMessage("'{PropertyValue}' is not a recommendation surface.");

        RuleForEach(command => command.TargetTypes!)
            .Must(type => Enum.TryParse<RecommendationTargetType>(type, ignoreCase: true, out _))
            .When(command => command.TargetTypes is not null)
            .WithMessage("'{PropertyValue}' is not a recommendation target type.");
    }
}
