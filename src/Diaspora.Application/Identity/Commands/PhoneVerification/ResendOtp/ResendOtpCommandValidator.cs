using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Validation;
using FluentValidation;

namespace Diaspora.Application.Identity.Commands.PhoneVerification.ResendOtp;

public sealed class ResendOtpCommandValidator : IdentityValidator<ResendOtpCommand>
{
    public ResendOtpCommandValidator(ILocalizedMessageProvider messages) : base(messages)
    {
        RuleFor(command => command.PhoneNumber)
            .NotEmpty().WithMessage(Message(IdentityMessageKeys.PhoneRequired));

        RuleFor(command => command.Purpose)
            .Must(value => IdentityValueParser.TryParseOtpPurpose(value, out _))
            .WithMessage(Message(IdentityMessageKeys.OtpPurposeUnknown));
    }
}
