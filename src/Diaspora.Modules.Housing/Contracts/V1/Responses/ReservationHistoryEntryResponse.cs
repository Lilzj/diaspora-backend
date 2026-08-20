namespace Diaspora.Modules.Housing.Contracts.V1.Responses;

/// <summary>One step in a reservation life, as it happened.</summary>
public sealed record ReservationHistoryEntryResponse
{
    public required Guid EntryId { get; init; }

    public required string Status { get; init; }

    /// <summary>Who did it. Null when the expiry sweep did.</summary>
    public Guid? ActorId { get; init; }

    public string? Reason { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }
}
