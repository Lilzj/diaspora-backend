using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Validation;
using FluentValidation;

namespace Diaspora.Application.Identity.Commands.Lifecycle.ReactivateAccount;

public sealed class ReactivateAccountCommandValidator : IdentityValidator<ReactivateAccountCommand>
{
    public ReactivateAccountCommandValidator(ILocalizedMessageProvider messages) : base(messages)
    {
        RuleFor(command => command.UserId)
            .NotEmpty().WithMessage(Message(IdentityMessageKeys.UserIdRequired));

        RuleFor(command => command.ReasonCode).MustBeReasonCode(Message);

        RuleFor(command => (string?)command.Note)
            .MustBeStaffNote(Message, IdentityMessageKeys.NoteRequired);
    }
}
