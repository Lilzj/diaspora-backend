using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Commands.StepUp;

/// <summary>
/// Redeems a step-up code and binds the grant to the calling session (BSEC-007).
/// <para>
/// <b>The scope is bound here, not when the code was requested.</b> A code proves the member holds
/// their phone; it says nothing about what they meant to do with that proof. Deciding at redemption
/// is what makes "a grant for one scope cannot satisfy another" true of the grant rather than of
/// the code, and it is also honest about the limit: an attacker who has somehow obtained a code can
/// name any scope, because that attacker has already defeated the factor. Scope bounds the blast
/// radius of a grant, not of a stolen code.
/// </para>
/// </summary>
[Authorize]
[Auditable(Action = "identity.stepup.completed")]
public sealed record CompleteStepUpCommand : ICommand<StepUpGrantResponse>
{
    public required string Scope { get; init; }

    public required string Code { get; init; }
}
