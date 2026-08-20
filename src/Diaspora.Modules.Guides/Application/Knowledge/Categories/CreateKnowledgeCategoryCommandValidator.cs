using Diaspora.Modules.Guides.Domain.Policies;
using FluentValidation;

namespace Diaspora.Modules.Guides.Application.Knowledge.Categories;

/// <summary>
/// Shape checks before the aggregate sees the request.
/// <para>
/// Deliberately shallow. The grammar of a key, the case of a locale and the collapse of duplicates
/// are the aggregate's rules and are enforced there, where they cannot be bypassed by a caller that
/// is not an HTTP request. What belongs here is the shape a client can get obviously wrong — a
/// missing name, forty thousand source keys — caught early enough to give a field-level message.
/// </para>
/// </summary>
public sealed class CreateKnowledgeCategoryCommandValidator
    : AbstractValidator<CreateKnowledgeCategoryCommand>
{
    public CreateKnowledgeCategoryCommandValidator()
    {
        RuleFor(command => command.Request).NotNull();

        RuleFor(command => command.Request.Key)
            .NotEmpty()
            .MaximumLength(KnowledgeLimits.MaxCategoryKey);

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
