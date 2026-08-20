using FluentValidation;

namespace Diaspora.Modules.Guides.Application.Engagement;

/// <summary>
/// Checks that a verdict was named. Which names are legal is <c>GuideWireNames</c>' answer, and
/// the handler asks it — a second copy of the vocabulary here is a second place for it to drift.
/// </summary>
public sealed class CastGuideVoteCommandValidator : AbstractValidator<CastGuideVoteCommand>
{
    public CastGuideVoteCommandValidator()
    {
        RuleFor(command => command.GuideId).NotEmpty();
        RuleFor(command => command.Request).NotNull();

        RuleFor(command => command.Request.Kind)
            .NotEmpty()
            .When(command => command.Request is not null);
    }
}
