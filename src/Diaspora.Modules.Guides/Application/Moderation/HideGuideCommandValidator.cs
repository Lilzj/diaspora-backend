using Diaspora.Modules.Guides.Domain.Policies;
using FluentValidation;

namespace Diaspora.Modules.Guides.Application.Moderation;

/// <summary>Insists on an explanation, and bounds it.</summary>
public sealed class HideGuideCommandValidator : AbstractValidator<HideGuideCommand>
{
    public HideGuideCommandValidator()
    {
        RuleFor(command => command.GuideId).NotEmpty();
        RuleFor(command => command.Request).NotNull();

        RuleFor(command => command.Request.Reason)
            .NotEmpty()
            .MaximumLength(GuideLimits.MaxModerationNote)
            .When(command => command.Request is not null);
    }
}
