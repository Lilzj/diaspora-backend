using Diaspora.Modules.Guides.Domain.Policies;
using FluentValidation;

namespace Diaspora.Modules.Guides.Application.Knowledge.Categories;

/// <summary>Shape checks. The grammar and the collapse rules belong to the aggregate.</summary>
public sealed class UpdateKnowledgeCategoryCommandValidator
    : AbstractValidator<UpdateKnowledgeCategoryCommand>
{
    public UpdateKnowledgeCategoryCommandValidator()
    {
        RuleFor(command => command.Key).NotEmpty();
        RuleFor(command => command.Request).NotNull();

        RuleFor(command => command.Request.Name)
            .NotEmpty()
            .MaximumLength(KnowledgeLimits.MaxCategoryName);

        RuleFor(command => command.Request.Description)
            .MaximumLength(KnowledgeLimits.MaxCategoryDescription);

        RuleFor(command => command.Request.Icon)
            .MaximumLength(KnowledgeLimits.MaxIcon);

        RuleFor(command => command.Request.SourceCategoryKeys)
            .Must(keys => keys.Count <= KnowledgeLimits.MaxCategorySources)
            .WithMessage("A navigation row cannot draw from that many filing categories.");

        RuleFor(command => command.Request.ProviderCategoryKeys)
            .Must(keys => keys.Count <= KnowledgeLimits.MaxProviderCategories)
            .WithMessage("A navigation row cannot recommend that many kinds of provider.");

        RuleFor(command => command.Request.Localizations)
            .Must(localizations => localizations.Count <= KnowledgeLimits.MaxLocalizations)
            .WithMessage("That is more translations than one row can carry.");
    }
}
