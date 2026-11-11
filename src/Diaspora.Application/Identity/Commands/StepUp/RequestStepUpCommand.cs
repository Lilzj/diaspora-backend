using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Commands.StepUp;

/// <summary>
/// Sends the signed-in member a code to confirm a sensitive operation (BSEC-007).
/// <para>
/// <b>Authentication alone, and that is the right gate.</b> Every signed-in member must be able
/// to reach this, because it is the only route past the step-up refusal; naming a permission here
/// would create a member who can be refused a withdrawal and cannot ask for the code that would
/// let them through. Spelled <c>[RequireAuthentication]</c> since BSOLID-001 replaced the bare
/// <c>[Authorize]</c> this originally carried (11 November 2026).
/// </para>
/// <para>
/// <b>What the scope does here, and what it does not.</b> It is checked against the catalogue
/// before any message is sent, so a client that names a scope the platform does not define learns
/// at once instead of after the member has read out a code that could never have worked. It is not
/// recorded: the code is proof of possession and nothing more, and what that proof is spent on is
/// decided when it is redeemed. The scope is sent twice because the client is doing two things,
/// not because the server remembers the first.
/// </para>
/// </summary>
[RequireAuthentication(
    "Naming a permission here would create a member who can be refused a withdrawal and "
    + "cannot ask for the code that would let them through.")]
[Auditable(Action = "identity.stepup.requested")]
public sealed record RequestStepUpCommand : ICommand<OtpChallengeResponse>
{
    public required string Scope { get; init; }
}
