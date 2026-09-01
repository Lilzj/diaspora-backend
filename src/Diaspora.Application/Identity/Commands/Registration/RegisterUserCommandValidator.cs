using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Validation;
using Diaspora.Domain.Identity.ValueObjects;
using Diaspora.SharedKernel.ValueObjects;
using FluentValidation;

namespace Diaspora.Application.Identity.Commands.Registration;

/// <summary>
/// Shape validation for registration. Password strength is not checked here — it belongs
/// to <c>IPasswordPolicy</c>, which the handler consults, so the rules stay in one place
/// and apply equally to registration, change and reset.
/// </summary>
public sealed class RegisterUserCommandValidator : IdentityValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator(ILocalizedMessageProvider messages) : base(messages)
    {
        RuleFor(command => command.FullName)
            .NotEmpty().WithMessage(Message(IdentityMessageKeys.FullNameRequired))
            .Must(value => FullName.TryCreate(value, out _))
            .WithMessage(Message(IdentityMessageKeys.FullNameLength));

        RuleFor(command => command.PhoneNumber)
            .NotEmpty().WithMessage(Message(IdentityMessageKeys.PhoneRequired))
            .Must(value => PhoneNumber.TryCreate(value, out _))
            .WithMessage(Message(IdentityMessageKeys.PhoneFormat));

        // The address is now the sign-in credential, so it is required at registration
        // and validated by the same value object that parses it everywhere else.
        RuleFor(command => command.Email)
            .NotEmpty().WithMessage(Message(IdentityMessageKeys.EmailRequired))
            .Must(value => EmailAddress.TryCreate(value, out _))
            .WithMessage(Message(IdentityMessageKeys.EmailFormat));

        RuleFor(command => command.Password)
            .NotEmpty().WithMessage(Message(IdentityMessageKeys.PasswordRequired));

        RuleFor(command => command.Nationality)
            .NotEmpty().WithMessage(Message(IdentityMessageKeys.NationalityRequired))
            .Must(value => CountryCode.TryCreate(value, out _))
            .WithMessage(Message(IdentityMessageKeys.NationalityFormat));

        RuleFor(command => command.CountryOfResidence)
            .NotEmpty().WithMessage(Message(IdentityMessageKeys.CountryOfResidenceRequired))
            .Must(value => CountryCode.TryCreate(value, out _))
            .WithMessage(Message(IdentityMessageKeys.CountryOfResidenceFormat));

        RuleFor(command => command.PreferredLanguage)
            .Must(value => LanguageCode.TryCreate(value, out _))
            .When(command => !string.IsNullOrWhiteSpace(command.PreferredLanguage))
            .WithMessage(Message(IdentityMessageKeys.LanguageFormat));

        RuleFor(command => command.PreferredTimeZone)
            .Must(value => TimeZoneIdentifier.TryCreate(value, out _))
            .When(command => !string.IsNullOrWhiteSpace(command.PreferredTimeZone))
            .WithMessage(Message(IdentityMessageKeys.TimeZoneFormat));
    }
}
