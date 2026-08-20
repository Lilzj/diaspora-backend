using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Commands.PhoneVerification.VerifyPhone;
using Diaspora.Application.Identity.Validation;
using FluentValidation;

namespace Diaspora.Application.Identity.Commands.Passwords.ResetPassword;

public sealed class ResetPasswordCommandValidator : IdentityValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator(ILocalizedMessageProvider messages) : base(messages)
    {
        RuleFor(command => command.PhoneNumber)
            .NotEmpty().WithMessage(Message(IdentityMessageKeys.PhoneRequired));

        RuleFor(command => command.Code)
            .NotEmpty().WithMessage(Message(IdentityMessageKeys.OtpRequired))
            .Must(code => code is not null &&
                          code.Trim().Length == VerifyPhoneCommandValidator.ExpectedCodeLength &&
                          code.Trim().All(char.IsAsciiDigit))
            .WithMessage(Message(
                IdentityMessageKeys.OtpFormat, VerifyPhoneCommandValidator.ExpectedCodeLength));

        RuleFor(command => command.NewPassword)
            .NotEmpty().WithMessage(Message(IdentityMessageKeys.PasswordRequired));
    }
}
