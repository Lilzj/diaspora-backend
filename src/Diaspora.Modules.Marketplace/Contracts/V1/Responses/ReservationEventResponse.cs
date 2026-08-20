namespace Diaspora.Modules.Marketplace.Contracts.V1.Responses;

/// <summary>One step in a reservation's life, as the history renders it.</summary>
public sealed record ReservationEventResponse
{
    /// <summary>
    /// <c>Pending</c>, <c>AwaitingCollection</c>, <c>Completed</c>, <c>Cancelled</c> or
    /// <c>Expired</c>.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>Why, when a party gave a reason.</summary>
    public string? Reason { get; init; }

    /// <summary>Whether a person or the clock caused it.</summary>
    public required bool WasAutomatic { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }
}
