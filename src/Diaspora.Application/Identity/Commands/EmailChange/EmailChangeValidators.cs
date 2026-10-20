using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Validation;
using FluentValidation;

namespace Diaspora.Application.Identity.Commands.EmailChange;

public sealed class RequestEmailChangeCommandValidator : IdentityValidator<RequestEmailChangeCommand>
{
    public RequestEmailChangeCommandValidator(ILocalizedMessageProvider messages) : base(messages)
    {
        RuleFor(command => command.Address)
            .NotEmpty().WithMessage(Message(IdentityMessageKeys.EmailRequired))
            .EmailAddress().WithMessage(Message(IdentityMessageKeys.EmailFormat));

        RuleFor(command => command.CurrentPassword)
            .NotEmpty().WithMessage(Message(IdentityMessageKeys.CurrentPasswordRequired));
    }
}
