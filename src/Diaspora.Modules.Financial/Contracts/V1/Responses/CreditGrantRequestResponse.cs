namespace Diaspora.Modules.Financial.Contracts.V1.Responses;

/// <summary>A credit grant waiting for, or decided by, a second Admin (7 Nov 2026). Staff.</summary>
public sealed record CreditGrantRequestResponse
{
    public required Guid RequestId { get; init; }

    public required Guid MemberUserId { get; init; }

    public required MoneyResponse Amount { get; init; }

    public required string Kind { get; init; }

    public required string Reason { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary><c>Pending</c>, <c>Approved</c>, <c>Rejected</c> or <c>Withdrawn</c>.</summary>
    public required string Status { get; init; }

    /// <summary>The Admin who asked. The grant is theirs.</summary>
    public required Guid RequestedByUserId { get; init; }

    public required DateTimeOffset RequestedAt { get; init; }

    /// <summary>The Admin who approved or rejected it. Null while pending, and on a withdrawal.</summary>
    public Guid? DecidedByUserId { get; init; }

    public DateTimeOffset? DecidedAt { get; init; }

    public string? DecisionNote { get; init; }

    /// <summary>The grant an approval produced.</summary>
    public Guid? CreditGrantId { get; init; }
}
