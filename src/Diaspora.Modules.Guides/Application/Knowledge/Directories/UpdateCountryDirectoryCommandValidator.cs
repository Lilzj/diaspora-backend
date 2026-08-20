using Diaspora.Modules.Guides.Domain.Policies;
using FluentValidation;

namespace Diaspora.Modules.Guides.Application.Knowledge.Directories;

/// <summary>Shape checks. Locale grammar and duplicate collapsing belong to the aggregate.</summary>
public sealed class UpdateCountryDirectoryCommandValidator
    : AbstractValidator<UpdateCountryDirectoryCommand>
{
    public UpdateCountryDirectoryCommandValidator()
    {
        RuleFor(command => command.CountryCode).NotEmpty().Length(2);
        RuleFor(command => command.Request).NotNull();

        RuleFor(command => command.Request.DisplayName)
            .NotEmpty()
            .MaximumLength(KnowledgeLimits.MaxDirectoryName);

        RuleFor(command => command.Request.Summary)
            .MaximumLength(KnowledgeLimits.MaxDirectorySummary);

        RuleFor(command => command.Request.Languages)
            .Must(languages => languages.Count <= KnowledgeLimits.MaxDirectoryLanguages)
            .WithMessage("That is more languages than one country directory can declare.");

        RuleFor(command => command.Request.Localizations)
            .Must(localizations => localizations.Count <= KnowledgeLimits.MaxLocalizations)
            .WithMessage("That is more translations than one directory can carry.");
    }
}
