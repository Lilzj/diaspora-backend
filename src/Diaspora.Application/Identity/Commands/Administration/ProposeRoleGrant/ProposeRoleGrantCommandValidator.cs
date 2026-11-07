using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Validation;
using FluentValidation;

namespace Diaspora.Application.Identity.Commands.Administration.ProposeRoleGrant;

public sealed class ProposeRoleGrantCommandValidator : IdentityValidator<ProposeRoleGrantCommand>
{
    public ProposeRoleGrantCommandValidator(ILocalizedMessageProvider messages) : base(messages)
    {
        RuleFor(command => command.UserId)
            .NotEmpty().WithMessage(Message(IdentityMessageKeys.UserIdRequired));

        RuleFor(command => command.Reason)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(Message(IdentityMessageKeys.ReasonRequired))
            .MaximumLength(StaffReasonRules.MaximumNoteLength).WithMessage(Message(IdentityMessageKeys.ReasonLength));
    }
}
