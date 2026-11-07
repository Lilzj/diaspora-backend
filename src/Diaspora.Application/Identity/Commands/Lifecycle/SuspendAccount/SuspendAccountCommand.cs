using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Authorization;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Commands.Lifecycle.SuspendAccount;

/// <summary>
/// Proposes suspending an account. Moderation action.
/// <para>
/// Since 7 Nov 2026 this suspends nobody: it records a proposal that a second member of staff
/// confirms (<c>ConfirmAccountActionCommand</c>), and the suspension happens then. The proposer
/// must be above the account in rank and give a reason code and a note the member will read.
/// </para>
/// </summary>
[Auditable(Action = "identity.user.suspension-proposed")]
[Authorize(Permission = IdentityPermissions.SuspendUser)]
public sealed record SuspendAccountCommand : ICommand<AccountActionProposalResponse>
{
    public required Guid UserId { get; init; }

    /// <summary>A moderation reason code's name.</summary>
    public required string ReasonCode { get; init; }

    /// <summary>Why, in the proposer's words. The member reads it.</summary>
    public required string Reason { get; init; }

    public DateTimeOffset? SuspendedUntil { get; init; }
}
