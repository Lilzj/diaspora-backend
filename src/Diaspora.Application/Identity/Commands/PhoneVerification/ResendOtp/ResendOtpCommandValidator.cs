using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Validation;
using Diaspora.Domain.Identity.Verification;
using FluentValidation;

namespace Diaspora.Application.Identity.Commands.PhoneVerification.ResendOtp;

public sealed class ResendOtpCommandValidator : IdentityValidator<ResendOtpCommand>
{
    public ResendOtpCommandValidator(ILocalizedMessageProvider messages) : base(messages)
    {
        RuleFor(command => command.PhoneNumber)
            .NotEmpty().WithMessage(Message(IdentityMessageKeys.PhoneRequired));

        // `SignIn` is refused: sign-in by code was removed on 5 Oct 2026, and with no challenge to
        // replace, a resend issues a fresh one, so this route would still text a code nobody
        // can redeem to any registered number.
        RuleFor(command => command.Purpose)
            .Must(value => IdentityValueParser.TryParseOtpPurpose(value, out var purpose)
                           && purpose != OtpPurpose.SignIn)
            .WithMessage(Message(IdentityMessageKeys.OtpPurposeUnknown));
    }
}
