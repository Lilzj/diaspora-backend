using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Validation;
using FluentValidation;

namespace Diaspora.Application.Identity.Commands.Passwords.ChangePassword;

public sealed class ChangePasswordCommandValidator : IdentityValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator(ILocalizedMessageProvider messages) : base(messages)
    {
        RuleFor(command => command.CurrentPassword)
            .NotEmpty().WithMessage(Message(IdentityMessageKeys.CurrentPasswordRequired));

        RuleFor(command => command.NewPassword)
            .NotEmpty().WithMessage(Message(IdentityMessageKeys.PasswordRequired));
    }
}
