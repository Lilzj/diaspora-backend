using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Validation;
using FluentValidation;

namespace Diaspora.Application.Identity.Commands.Lifecycle.ReinstateAccount;

public sealed class ReinstateAccountCommandValidator : IdentityValidator<ReinstateAccountCommand>
{
    public ReinstateAccountCommandValidator(ILocalizedMessageProvider messages) : base(messages)
    {
        RuleFor(command => command.UserId)
            .NotEmpty().WithMessage(Message(IdentityMessageKeys.UserIdRequired));

        // Required since 7 Nov 2026: no staff action on a member without a reason.
        RuleFor(command => command.Notes)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(Message(IdentityMessageKeys.NoteRequired))
            .MaximumLength(1000).WithMessage(Message(IdentityMessageKeys.NotesLength));
    }
}
