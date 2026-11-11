using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Authorization;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Commands.Lifecycle.DisableAccount;

/// <summary>
/// Proposes closing an account.
/// <para>
/// Since 7 Nov 2026 this closes nothing: it records a proposal a second member of staff confirms,
/// and then sessions end and every issued token stops working.
/// </para>
/// </summary>
[Auditable(Action = "identity.user.closure-proposed")]
[RequirePermission(IdentityPermissions.DisableUser)]
public sealed record DisableAccountCommand : ICommand<AccountActionProposalResponse>
{
    public required Guid UserId { get; init; }

    /// <summary>A moderation reason code's name.</summary>
    public required string ReasonCode { get; init; }

    /// <summary>Why, in the proposer's words. The member reads it.</summary>
    public required string Reason { get; init; }
}
