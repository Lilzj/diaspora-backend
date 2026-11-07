using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Validation;
using FluentValidation;

namespace Diaspora.Application.Identity.Commands.Administration.RevokeMemberSessions;

public sealed class RevokeMemberSessionsCommandValidator : IdentityValidator<RevokeMemberSessionsCommand>
{
    public RevokeMemberSessionsCommandValidator(ILocalizedMessageProvider messages) : base(messages)
    {
        RuleFor(command => command.UserId)
            .NotEmpty().WithMessage(Message(IdentityMessageKeys.UserIdRequired));

        // Required since 7 Nov 2026: no staff action on a member without a reason.
        RuleFor(command => command.Reason)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(Message(IdentityMessageKeys.ReasonRequired))
            .MaximumLength(StaffReasonRules.MaximumNoteLength).WithMessage(Message(IdentityMessageKeys.ReasonLength));
    }
}
