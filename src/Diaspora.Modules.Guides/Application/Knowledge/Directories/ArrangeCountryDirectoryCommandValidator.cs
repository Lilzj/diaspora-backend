using Diaspora.Modules.Guides.Domain.Policies;
using FluentValidation;

namespace Diaspora.Modules.Guides.Application.Knowledge.Directories;

/// <summary>Shape checks. Duplicate detection and renumbering belong to the aggregate.</summary>
public sealed class ArrangeCountryDirectoryCommandValidator
    : AbstractValidator<ArrangeCountryDirectoryCommand>
{
    public ArrangeCountryDirectoryCommandValidator()
    {
        RuleFor(command => command.CountryCode).NotEmpty().Length(2);
        RuleFor(command => command.Request).NotNull();

        RuleFor(command => command.Request.Entries)
            .Must(entries => entries.Count <= KnowledgeLimits.MaxDirectoryEntries)
            .WithMessage("That is more categories than one country directory can list.");

        RuleForEach(command => command.Request.Entries).ChildRules(entry =>
        {
            entry.RuleFor(value => value.KnowledgeCategoryKey)
                .NotEmpty()
                .MaximumLength(KnowledgeLimits.MaxCategoryKey);

            entry.RuleFor(value => value.LabelOverride)
                .MaximumLength(KnowledgeLimits.MaxCategoryName);
        });
    }
}
