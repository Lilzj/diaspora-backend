using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Validation;
using Diaspora.Domain.Identity.ValueObjects;
using FluentValidation;

namespace Diaspora.Application.Identity.Commands.Profile.UpdateProfile;

public sealed class UpdateProfileCommandValidator : IdentityValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator(ILocalizedMessageProvider messages) : base(messages)
    {
        RuleFor(command => command.FullName)
            .NotEmpty().WithMessage(Message(IdentityMessageKeys.FullNameRequired))
            .Must(value => FullName.TryCreate(value, out _))
            .WithMessage(Message(IdentityMessageKeys.FullNameLength));

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
            .WithMessage(Message(IdentityMessageKeys.LanguageFormat));

        RuleFor(command => command.PreferredTimeZone)
            .Must(value => TimeZoneIdentifier.TryCreate(value, out _))
            .WithMessage(Message(IdentityMessageKeys.TimeZoneFormat));

        RuleFor(command => command.Email)
            .EmailAddress().WithMessage(Message(IdentityMessageKeys.EmailFormat))
            .When(command => !string.IsNullOrWhiteSpace(command.Email));
    }
}
