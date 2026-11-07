using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Validation;
using FluentValidation;

namespace Diaspora.Application.Identity.Commands.Lifecycle.SuspendAccount;

public sealed class SuspendAccountCommandValidator : IdentityValidator<SuspendAccountCommand>
{
    public SuspendAccountCommandValidator(ILocalizedMessageProvider messages) : base(messages)
    {
        RuleFor(command => command.UserId)
            .NotEmpty().WithMessage(Message(IdentityMessageKeys.UserIdRequired));

        RuleFor(command => command.ReasonCode).MustBeReasonCode(Message);

        RuleFor(command => (string?)command.Reason).MustBeStaffNote(Message);
    }
}
