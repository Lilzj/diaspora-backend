using Diaspora.Modules.Guides.Domain.Policies;
using FluentValidation;

namespace Diaspora.Modules.Guides.Application.Knowledge.Directories;

/// <summary>Shape checks. Country-code and locale grammar belong to the aggregate.</summary>
public sealed class CreateCountryDirectoryCommandValidator
    : AbstractValidator<CreateCountryDirectoryCommand>
{
    public CreateCountryDirectoryCommandValidator()
    {
        RuleFor(command => command.CountryCode).NotEmpty().Length(2);
        RuleFor(command => command.Request).NotNull();

        RuleFor(command => command.Request.DisplayName)
            .MaximumLength(KnowledgeLimits.MaxDirectoryName);

        RuleFor(command => command.Request.Summary)
            .MaximumLength(KnowledgeLimits.MaxDirectorySummary);

        RuleFor(command => command.Request.Languages)
            .Must(languages => languages.Count <= KnowledgeLimits.MaxDirectoryLanguages)
            .WithMessage("That is more languages than one country directory can declare.");
    }
}
