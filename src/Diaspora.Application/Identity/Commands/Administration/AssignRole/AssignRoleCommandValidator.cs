using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Validation;
using FluentValidation;

namespace Diaspora.Application.Identity.Commands.Administration.AssignRole;

public sealed class AssignRoleCommandValidator : IdentityValidator<AssignRoleCommand>
{
    public AssignRoleCommandValidator(ILocalizedMessageProvider messages) : base(messages)
    {
        RuleFor(command => command.UserId)
            .NotEmpty().WithMessage(Message(IdentityMessageKeys.UserIdRequired));

        // Required since 7 Nov 2026: the member is told why their role changed.
        RuleFor(command => command.Reason)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(Message(IdentityMessageKeys.ReasonRequired))
            .MaximumLength(StaffReasonRules.MaximumNoteLength).WithMessage(Message(IdentityMessageKeys.ReasonLength));
    }
}
