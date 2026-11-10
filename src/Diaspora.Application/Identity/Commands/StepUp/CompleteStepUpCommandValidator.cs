using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Validation;
using FluentValidation;

namespace Diaspora.Application.Identity.Commands.StepUp;

public sealed class CompleteStepUpCommandValidator : IdentityValidator<CompleteStepUpCommand>
{
    public CompleteStepUpCommandValidator(ILocalizedMessageProvider messages) : base(messages)
    {
        RuleFor(command => command.Scope)
            .Must(scope => scope is not null && StepUpScopes.All.Contains(scope))
            .WithMessage(Message(IdentityMessageKeys.StepUpScopeUnknown));

        RuleFor(command => command.Code)
            .NotEmpty().WithMessage(Message(IdentityMessageKeys.OtpRequired));
    }
}
