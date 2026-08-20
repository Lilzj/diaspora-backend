using Diaspora.Modules.Guides.Domain.Policies;
using FluentValidation;

namespace Diaspora.Modules.Guides.Application.Suggestions;

/// <summary>
/// Bounds the proposal. The rationale is bounded but not refused at the ceiling — the aggregate
/// truncates it, because losing a tail nobody will read is better than losing the suggestion.
/// </summary>
public sealed class SubmitGuideSuggestionCommandValidator
    : AbstractValidator<SubmitGuideSuggestionCommand>
{
    public SubmitGuideSuggestionCommandValidator()
    {
        RuleFor(command => command.GuideId).NotEmpty();
        RuleFor(command => command.Request).NotNull();

        When(command => command.Request is not null, () =>
        {
            RuleFor(command => command.Request.ProposedContent)
                .NotEmpty()
                .MaximumLength(GuideLimits.MaxSuggestionContent);
        });
    }
}
