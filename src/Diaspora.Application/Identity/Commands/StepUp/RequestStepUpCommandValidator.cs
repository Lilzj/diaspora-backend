using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Validation;
using FluentValidation;

namespace Diaspora.Application.Identity.Commands.StepUp;

/// <summary>
/// Refuses a scope the platform does not define, before an SMS is sent.
/// </summary>
/// <remarks>
/// The check is <see cref="StepUpScopes.All"/> and not a format rule, deliberately. A scope is only
/// ever one of a short closed list the gate also reads, so anything else is a client bug or a
/// probe, and both deserve the same answer.
/// </remarks>
public sealed class RequestStepUpCommandValidator : IdentityValidator<RequestStepUpCommand>
{
    public RequestStepUpCommandValidator(ILocalizedMessageProvider messages) : base(messages)
    {
        RuleFor(command => command.Scope)
            .Must(scope => scope is not null && StepUpScopes.All.Contains(scope))
            .WithMessage(Message(IdentityMessageKeys.StepUpScopeUnknown));
    }
}
