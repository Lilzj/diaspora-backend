using Diaspora.Modules.Location.Application.Experience;
using Diaspora.Modules.Location.Domain.Experience;
using FluentValidation;

namespace Diaspora.Modules.Location.Application.Validation;

/// <summary>
/// Shape validation for a declared residence.
/// <para>
/// Shape only. Whether the period is plausible, whether it overlaps another, and what
/// experience level it earns are the aggregate's rules and stay there — a validator that
/// duplicated them would be a second place to change when they move, and the one nobody
/// remembers.
/// </para>
/// </summary>
public sealed class DeclareCountryExperienceValidator
    : AbstractValidator<DeclareCountryExperienceCommand>
{
    public DeclareCountryExperienceValidator()
    {
        RuleFor(command => command.Experience).NotNull();

        RuleFor(command => command.Experience.CountryCode)
            .NotEmpty()
            .Length(2)
            .WithMessage("A country code must be two letters (ISO 3166-1 alpha-2).");

        RuleFor(command => command.Experience.Role)
            .MaximumLength(CountryExperience.MaxRoleLength);

        RuleFor(command => command.Experience.ExpertiseAreas!)
            .Must(areas => areas.Count <= CountryExperience.MaxExpertiseAreas)
            .When(command => command.Experience.ExpertiseAreas is not null)
            .WithMessage(
                $"You can claim at most {CountryExperience.MaxExpertiseAreas} expertise areas per country.");
    }
}
