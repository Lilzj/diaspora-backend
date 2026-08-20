using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Validation;
using FluentValidation;

namespace Diaspora.Application.Identity.Commands.Authentication.RefreshToken;

public sealed class RefreshTokenCommandValidator : IdentityValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator(ILocalizedMessageProvider messages) : base(messages)
    {
        RuleFor(command => command.RefreshToken)
            .NotEmpty().WithMessage(Message(IdentityMessageKeys.RefreshTokenRequired));
    }
}
