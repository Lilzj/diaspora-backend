using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Validation;
using FluentValidation;

namespace Diaspora.Application.Identity.Commands.Verification.SubmitVerificationRequest;

public sealed class SubmitVerificationRequestCommandValidator
    : IdentityValidator<SubmitVerificationRequestCommand>
{
    public SubmitVerificationRequestCommandValidator(ILocalizedMessageProvider messages) : base(messages)
    {
        RuleFor(command => command.Notes)
            .MaximumLength(1000).WithMessage(Message(IdentityMessageKeys.NotesLength))
            .When(command => !string.IsNullOrWhiteSpace(command.Notes));
    }
}
