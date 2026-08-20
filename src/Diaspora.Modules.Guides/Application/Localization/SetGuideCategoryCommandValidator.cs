using Diaspora.Modules.Guides.Domain.Policies;
using FluentValidation;

namespace Diaspora.Modules.Guides.Application.Localization;

/// <summary>
/// Checks that a key was supplied and is short enough to be one. Whether it names a real, active
/// category is the catalogue's answer, and the handler asks it.
/// </summary>
public sealed class SetGuideCategoryCommandValidator : AbstractValidator<SetGuideCategoryCommand>
{
    public SetGuideCategoryCommandValidator()
    {
        RuleFor(command => command.GuideId).NotEmpty();
        RuleFor(command => command.Request).NotNull();

        When(command => command.Request is not null, () =>
        {
            RuleFor(command => command.Request.CategoryKey)
                .NotEmpty()
                .MaximumLength(GuideLimits.MaxCategoryKey);

            RuleFor(command => command.Request.BaseRevision)
                .GreaterThanOrEqualTo(0)
                .When(command => command.Request.BaseRevision is not null);
        });
    }
}
