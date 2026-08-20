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

        RuleFor(command => command.Notes)
            .MaximumLength(1000).WithMessage(Message(IdentityMessageKeys.NotesLength))
            .When(command => !string.IsNullOrWhiteSpace(command.Notes));
    }
}
