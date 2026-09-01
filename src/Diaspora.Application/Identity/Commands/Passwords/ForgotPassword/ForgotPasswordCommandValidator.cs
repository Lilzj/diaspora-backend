using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Validation;
using FluentValidation;

namespace Diaspora.Application.Identity.Commands.Passwords.ForgotPassword;

public sealed class ForgotPasswordCommandValidator : IdentityValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator(ILocalizedMessageProvider messages) : base(messages)
    {
        RuleFor(command => command.Email)
            .NotEmpty().WithMessage(Message(IdentityMessageKeys.EmailRequired))
            .EmailAddress().WithMessage(Message(IdentityMessageKeys.EmailFormat));
    }
}
