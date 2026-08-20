namespace Diaspora.Modules.Financial.Contracts.V1.Responses;

/// <summary>One transaction, as a member sees it in their history.</summary>
public sealed record TransactionResponse
{
    public required Guid TransactionId { get; init; }

    public required string TransactionType { get; init; }

    public required string Status { get; init; }

    public required MoneyResponse Amount { get; init; }

    public required string Description { get; init; }

    public required string Reason { get; init; }

    public string? SubjectType { get; init; }

    public Guid? SubjectId { get; init; }

    public string? CountryCode { get; init; }

    public string? PolicyVersion { get; init; }

    public required int EntryCount { get; init; }

    /// <summary>Set when a compensating transaction undid this one.</summary>
    public Guid? ReversedByTransactionId { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }
}
