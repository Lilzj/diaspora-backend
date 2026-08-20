using Diaspora.Modules.Guides.Domain.Policies;
using FluentValidation;

namespace Diaspora.Modules.Guides.Application.Localization;

/// <summary>Checks the target language and the optional overrides a translation may carry.</summary>
public sealed class CreateGuideTranslationCommandValidator
    : AbstractValidator<CreateGuideTranslationCommand>
{
    public CreateGuideTranslationCommandValidator()
    {
        RuleFor(command => command.GuideId).NotEmpty();
        RuleFor(command => command.Request).NotNull();

        When(command => command.Request is not null, () =>
        {
            RuleFor(command => command.Request.Language)
                .NotEmpty()
                .Length(2);

            RuleFor(command => command.Request.Title)
                .MinimumLength(GuideLimits.MinTitle)
                .MaximumLength(GuideLimits.MaxTitle)
                .When(command => !string.IsNullOrWhiteSpace(command.Request.Title));

            RuleFor(command => command.Request.Slug)
                .MaximumLength(GuideLimits.MaxSlugLength);
        });
    }
}
