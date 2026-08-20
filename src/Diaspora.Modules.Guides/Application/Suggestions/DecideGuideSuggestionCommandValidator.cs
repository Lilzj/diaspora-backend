using Diaspora.Modules.Guides.Domain.Policies;
using FluentValidation;

namespace Diaspora.Modules.Guides.Application.Suggestions;

/// <summary>Bounds the note the suggester will read.</summary>
public sealed class DecideGuideSuggestionCommandValidator
    : AbstractValidator<DecideGuideSuggestionCommand>
{
    public DecideGuideSuggestionCommandValidator()
    {
        RuleFor(command => command.SuggestionId).NotEmpty();
        RuleFor(command => command.Request).NotNull();

        RuleFor(command => command.Request.Note)
            .MaximumLength(GuideLimits.MaxSuggestionRationale)
            .When(command => command.Request is not null);
    }
}
