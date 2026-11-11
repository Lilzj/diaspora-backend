using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Commands.Verification.ApplyTrustDecision;

/// <summary>
/// Applies a verdict from the Trust &amp; Verification Engine (Phase B3).
/// <para>
/// The only command in the module that can move a member to <c>Verified</c>, and it is
/// gated on a permission no human role holds. That is the mechanical expression of the
/// architectural split: Identity owns authentication and access state, the Trust Engine
/// owns whether someone is trustworthy, and neither can quietly assume the other's job.
/// </para>
/// </summary>
[Auditable(Action = "identity.user.trust-decision-applied")]
[RequirePermission("identity:trust:apply-decision")]
public sealed record ApplyTrustDecisionCommand : ICommand<AccessStateResponse>
{
    public required Guid UserId { get; init; }

    public required string Outcome { get; init; }

    public string? Reason { get; init; }

    public string? DecidedBy { get; init; }
}
