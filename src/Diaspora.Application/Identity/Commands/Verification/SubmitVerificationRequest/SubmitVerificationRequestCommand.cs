using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Commands.Verification.SubmitVerificationRequest;

/// <summary>
/// Records that a member has asked to be verified.
/// <para>
/// This is the boundary of Identity's authority over verification. It may note that a
/// request exists and move the member to <c>VerificationPending</c>; it may not decide
/// the outcome. The verdict arrives from the Trust &amp; Verification Engine through
/// <c>ApplyTrustDecisionCommand</c>.
/// </para>
/// </summary>
[Auditable(Action = "identity.user.verification-requested")]
[Authorize(Permission = "identity:verification:submit")]
public sealed record SubmitVerificationRequestCommand : ICommand<AccessStateResponse>
{
    public string? Notes { get; init; }
}
