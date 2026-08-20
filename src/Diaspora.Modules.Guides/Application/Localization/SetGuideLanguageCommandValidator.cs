using FluentValidation;

namespace Diaspora.Modules.Guides.Application.Localization;

/// <summary>
/// Checks the shape of the code. Whether the two letters name a language anyone speaks is the
/// value object's question, and it answers it the same way for every caller.
/// </summary>
public sealed class SetGuideLanguageCommandValidator : AbstractValidator<SetGuideLanguageCommand>
{
    public SetGuideLanguageCommandValidator()
    {
        RuleFor(command => command.GuideId).NotEmpty();
        RuleFor(command => command.Request).NotNull();

        When(command => command.Request is not null, () =>
        {
            RuleFor(command => command.Request.Language)
                .NotEmpty()
                .Length(2);

            RuleFor(command => command.Request.BaseRevision)
                .GreaterThanOrEqualTo(0)
                .When(command => command.Request.BaseRevision is not null);
        });
    }
}
