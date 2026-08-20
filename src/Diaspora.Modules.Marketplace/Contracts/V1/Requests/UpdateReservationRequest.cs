namespace Diaspora.Modules.Marketplace.Contracts.V1.Requests;

/// <summary>
/// Moves a reservation along.
/// <para>
/// One request for the whole hand-off, because the four transitions differ only in which word the
/// caller sends and in which party may send it. Acknowledging is the seller's; completing is
/// either party's once they have met; cancelling is both parties' at any point, and is never
/// refused.
/// </para>
/// </summary>
public sealed record UpdateReservationRequest
{
    /// <summary><c>Acknowledge</c>, <c>Complete</c> or <c>Cancel</c>.</summary>
    public required string Action { get; init; }

    /// <summary>On an acknowledgement, the collection window the seller is offering.</summary>
    public DateTimeOffset? CollectFrom { get; init; }

    public DateTimeOffset? CollectUntil { get; init; }

    public string? CollectionPlace { get; init; }

    /// <summary>Why, on a cancellation. Shown to the other party, who is owed one.</summary>
    public string? Reason { get; init; }
}
