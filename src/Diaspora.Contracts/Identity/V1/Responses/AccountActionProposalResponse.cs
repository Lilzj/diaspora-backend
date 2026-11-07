namespace Diaspora.Contracts.Identity.V1.Responses;

/// <summary>
/// An account action one member of staff proposed and a second decides (7 Nov 2026): suspending,
/// closing or reopening an account, or granting Admin or SuperAdmin.
/// </summary>
public sealed record AccountActionProposalResponse
{
    public required Guid Id { get; init; }

    /// <summary><c>Suspend</c>, <c>Disable</c>, <c>GrantRole</c> or <c>Reactivate</c>.</summary>
    public required string Kind { get; init; }

    public required Guid TargetUserId { get; init; }

    /// <summary>The account's name now, for the queue. Null if the account cannot be read.</summary>
    public string? TargetFullName { get; init; }

    /// <summary>The account's role now.</summary>
    public string? TargetRole { get; init; }

    public required Guid ProposedBy { get; init; }

    public string? ProposedByName { get; init; }

    /// <summary>A moderation reason code's name. Null for a role grant.</summary>
    public string? ReasonCode { get; init; }

    /// <summary>The reason as the member reads it, for example "a fraud or scam".</summary>
    public string? ReasonLabel { get; init; }

    public required string Note { get; init; }

    /// <summary>When a proposed suspension would end. Null means until reviewed.</summary>
    public DateTimeOffset? Until { get; init; }

    /// <summary>The role a <c>GrantRole</c> proposal gives.</summary>
    public string? Role { get; init; }

    /// <summary><c>Pending</c>, <c>Confirmed</c>, <c>Declined</c> or <c>Withdrawn</c>.</summary>
    public required string Status { get; init; }

    public Guid? DecidedBy { get; init; }

    public string? DecidedByName { get; init; }

    public DateTimeOffset? DecidedAt { get; init; }

    public string? DecisionNote { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
