namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>
/// One entry on an application's timeline.
/// <para>
/// The candidate's view is filtered to entries marked visible to them. An employer's internal
/// note — "strong, but we already have someone" — belongs on the record and emphatically not in
/// front of the candidate, and the flag on the domain entity is what makes an honest record and
/// a kind timeline possible at once.
/// </para>
/// </summary>
public sealed record ApplicationHistoryResponse
{
    public required Guid EntryId { get; init; }

    /// <summary><c>Submitted</c>, <c>StatusChanged</c>, <c>InterviewScheduled</c>, …</summary>
    public required string Kind { get; init; }

    public string? FromStatus { get; init; }

    public string? ToStatus { get; init; }

    public string? Note { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }
}
