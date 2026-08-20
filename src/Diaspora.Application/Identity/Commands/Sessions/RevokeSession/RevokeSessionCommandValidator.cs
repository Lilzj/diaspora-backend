using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Validation;
using FluentValidation;

namespace Diaspora.Application.Identity.Commands.Sessions.RevokeSession;

public sealed class RevokeSessionCommandValidator : IdentityValidator<RevokeSessionCommand>
{
    public RevokeSessionCommandValidator(ILocalizedMessageProvider messages) : base(messages)
    {
        RuleFor(command => command.SessionId)
            .NotEmpty().WithMessage(Message(IdentityMessageKeys.UserIdRequired));
    }
}
