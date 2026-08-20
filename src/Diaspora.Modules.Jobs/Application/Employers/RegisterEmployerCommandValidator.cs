using Diaspora.Modules.Jobs.Domain.Policies;
using FluentValidation;

namespace Diaspora.Modules.Jobs.Application.Employers;

/// <summary>
/// Rejects a registration the domain would refuse anyway, before a database round trip.
/// <para>
/// Shape only. Whether the slug is free and whether the organisation type is a real one are
/// uniqueness and catalogue questions the handler asks — a validator that answered them would
/// need repositories and would answer them again a moment later.
/// </para>
/// </summary>
public sealed class RegisterEmployerCommandValidator
    : AbstractValidator<RegisterEmployerCommand>
{
    public RegisterEmployerCommandValidator()
    {
        RuleFor(command => command.Request).NotNull();

        When(command => command.Request is not null, () =>
        {
            RuleFor(command => command.Request.DisplayName)
                .NotEmpty()
                .MinimumLength(JobLimits.MinEmployerName)
                .MaximumLength(JobLimits.MaxEmployerName);

            RuleFor(command => command.Request.LegalName)
                .MaximumLength(JobLimits.MaxEmployerName);

            RuleFor(command => command.Request.RegistrationNumber)
                .MaximumLength(JobLimits.MaxRegistrationNumber);

            RuleFor(command => command.Request.OrganizationType).NotEmpty();

            RuleFor(command => command.Request.CountryCode)
                .NotEmpty()
                .Length(2);

            RuleFor(command => command.Request.Slug)
                .MaximumLength(JobLimits.MaxSlug);

            RuleFor(command => command.Request.Description)
                .MaximumLength(JobLimits.MaxEmployerDescription);
        });
    }
}
