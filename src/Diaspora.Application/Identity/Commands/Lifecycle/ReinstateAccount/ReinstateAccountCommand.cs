using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Commands.Lifecycle.ReinstateAccount;

/// <summary>
/// Lifts a suspension or hard lock, restoring the access rung the member held before it.
/// <para>
/// Taken at once by one member of staff, above the account in rank, with a note the member is
/// told (7 Nov 2026). Lifting a restriction is the step back, so it does not wait for a second.
/// </para>
/// </summary>
[Auditable(Action = "identity.user.reinstated")]
[Authorize(Permission = "identity:users:reinstate")]
public sealed record ReinstateAccountCommand : ICommand<OperationAcknowledgedResponse>
{
    public required Guid UserId { get; init; }

    /// <summary>Why. Required since 7 Nov 2026.</summary>
    public string? Notes { get; init; }
}
