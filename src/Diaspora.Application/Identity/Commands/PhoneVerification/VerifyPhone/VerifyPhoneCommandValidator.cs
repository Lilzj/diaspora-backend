using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Validation;
using Diaspora.SharedKernel.ValueObjects;
using FluentValidation;

namespace Diaspora.Application.Identity.Commands.PhoneVerification.VerifyPhone;

public sealed class VerifyPhoneCommandValidator : IdentityValidator<VerifyPhoneCommand>
{
    /// <summary>Kept in step with the code generator's output length.</summary>
    public const int ExpectedCodeLength = 6;

    public VerifyPhoneCommandValidator(ILocalizedMessageProvider messages) : base(messages)
    {
        RuleFor(command => command.PhoneNumber)
            .NotEmpty().WithMessage(Message(IdentityMessageKeys.PhoneRequired))
            .Must(value => PhoneNumber.TryCreate(value, out _))
            .WithMessage(Message(IdentityMessageKeys.PhoneFormat));

        RuleFor(command => command.Code)
            .NotEmpty().WithMessage(Message(IdentityMessageKeys.OtpRequired))
            .Must(code => code is not null &&
                          code.Trim().Length == ExpectedCodeLength &&
                          code.Trim().All(char.IsAsciiDigit))
            .WithMessage(Message(IdentityMessageKeys.OtpFormat, ExpectedCodeLength));
    }
}
