using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Authorization;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Commands.Lifecycle.ReactivateAccount;

/// <summary>
/// Proposes reopening a closed account (7 Nov 2026).
/// <para>
/// How a closed member's appeal is honoured: until then <c>User.Reactivate</c> had no caller and a
/// closed account stayed closed whatever an appeal found. A second member of staff confirms it, and
/// the member returns at limited access. Needs the permission to close an account: only those who
/// may close one may reopen one.
/// </para>
/// </summary>
[Auditable(Action = "identity.user.reopening-proposed")]
[Authorize(Permission = IdentityPermissions.DisableUser)]
public sealed record ReactivateAccountCommand : ICommand<AccountActionProposalResponse>
{
    public required Guid UserId { get; init; }

    /// <summary>A moderation reason code's name.</summary>
    public required string ReasonCode { get; init; }

    /// <summary>Why it is reopened. The member reads it.</summary>
    public required string Note { get; init; }
}
