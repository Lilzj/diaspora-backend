namespace Diaspora.Modules.Financial.Contracts.V1.Responses;

/// <summary>
/// One line of a statement.
/// <para>
/// <b><see cref="BalanceAfter"/> is what was stored at the time, not a running total recomputed on
/// read.</b> A statement whose figures change when the code does is not a statement.
/// </para>
/// <para>
/// The audit fields are on the member-facing response on purpose. Somebody looking at a charge they
/// do not recognise should be able to see who caused it, why, and which policy version priced it,
/// without opening a support ticket.
/// </para>
/// </summary>
public sealed record LedgerEntryResponse
{
    public required Guid EntryId { get; init; }

    public required Guid TransactionId { get; init; }

    /// <summary>Position in this wallet's statement. Gapless, and what paging uses.</summary>
    public required long Sequence { get; init; }

    /// <summary><c>Payment</c>, <c>Refund</c>, <c>Commission</c>, <c>Hold</c>, and so on.</summary>
    public required string EntryType { get; init; }

    /// <summary><c>Debit</c> or <c>Credit</c>, from the owner's point of view.</summary>
    public required string Direction { get; init; }

    /// <summary><c>Available</c>, <c>Pending</c> or <c>Reserved</c>.</summary>
    public required string Bucket { get; init; }

    public required MoneyResponse Amount { get; init; }

    public required MoneyResponse BalanceAfter { get; init; }

    /// <summary>Why, in words. Never empty.</summary>
    public required string Reason { get; init; }

    /// <summary><c>Member</c>, <c>Staff</c>, <c>System</c> or <c>Gateway</c>.</summary>
    public required string ActorType { get; init; }

    /// <summary><c>Api</c>, <c>Webhook</c>, <c>System</c>, <c>Operator</c> or <c>Migration</c>.</summary>
    public required string Source { get; init; }

    /// <summary>The policy version that decided the amount, when a policy did.</summary>
    public string? PolicyVersion { get; init; }

    public string? CorrelationId { get; init; }

    public string? SubjectType { get; init; }

    public Guid? SubjectId { get; init; }

    public string? ExternalReference { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }
}
