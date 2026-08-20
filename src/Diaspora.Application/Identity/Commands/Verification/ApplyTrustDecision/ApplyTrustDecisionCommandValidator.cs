using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Validation;
using FluentValidation;

namespace Diaspora.Application.Identity.Commands.Verification.ApplyTrustDecision;

public sealed class ApplyTrustDecisionCommandValidator : IdentityValidator<ApplyTrustDecisionCommand>
{
    public ApplyTrustDecisionCommandValidator(ILocalizedMessageProvider messages) : base(messages)
    {
        RuleFor(command => command.UserId)
            .NotEmpty().WithMessage(Message(IdentityMessageKeys.UserIdRequired));

        RuleFor(command => command.Outcome)
            .Must(value => IdentityValueParser.TryParseTrustOutcome(value, out _))
            .WithMessage(Message(IdentityMessageKeys.TrustOutcomeUnknown));

        RuleFor(command => command.Reason)
            .MaximumLength(512).WithMessage(Message(IdentityMessageKeys.ReasonLength))
            .When(command => !string.IsNullOrWhiteSpace(command.Reason));
    }
}
