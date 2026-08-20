using Diaspora.Modules.Guides.Domain.Policies;
using FluentValidation;

namespace Diaspora.Modules.Guides.Application.Categories;

/// <summary>Bounds an edit to a catalogue entry.</summary>
public sealed class UpdateGuideCategoryCommandValidator
    : AbstractValidator<UpdateGuideCategoryCommand>
{
    public UpdateGuideCategoryCommandValidator()
    {
        RuleFor(command => command.Key)
            .NotEmpty()
            .MaximumLength(GuideLimits.MaxCategoryKey);

        RuleFor(command => command.Request).NotNull();

        When(command => command.Request is not null, () =>
        {
            RuleFor(command => command.Request.Name)
                .NotEmpty()
                .MaximumLength(GuideLimits.MaxCategoryName);

            RuleFor(command => command.Request.Description)
                .MaximumLength(GuideLimits.MaxCategoryDescription);

            RuleFor(command => command.Request.Icon)
                .MaximumLength(GuideLimits.MaxCategoryName);

            RuleFor(command => command.Request.ParentKey)
                .MaximumLength(GuideLimits.MaxCategoryKey);

            RuleFor(command => command.Request.Order).GreaterThanOrEqualTo(0);
        });
    }
}
