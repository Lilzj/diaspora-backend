using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Validation;
using FluentValidation;

namespace Diaspora.Application.Identity.Commands.Authentication.Login;

/// <summary>
/// Presence checks only.
/// <para>
/// Sign-in deliberately does not validate the shape of the phone number or password
/// beyond emptiness. A "that is not a valid phone number" response on the login endpoint
/// would tell an attacker which formats are worth trying, and password rules must never
/// be inferable from the sign-in path.
/// </para>
/// </summary>
public sealed class LoginCommandValidator : IdentityValidator<LoginCommand>
{
    public LoginCommandValidator(ILocalizedMessageProvider messages) : base(messages)
    {
        RuleFor(command => command.Email)
            .NotEmpty().WithMessage(Message(IdentityMessageKeys.EmailRequired))
            .EmailAddress().WithMessage(Message(IdentityMessageKeys.EmailFormat));

        RuleFor(command => command.Password)
            .NotEmpty().WithMessage(Message(IdentityMessageKeys.PasswordRequired));

        RuleFor(command => command.DeviceIdentifier)
            .MaximumLength(200).WithMessage(Message(IdentityMessageKeys.DeviceIdentifierLength))
            .When(command => !string.IsNullOrWhiteSpace(command.DeviceIdentifier));
    }
}
