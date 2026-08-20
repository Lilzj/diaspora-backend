namespace Diaspora.Modules.Marketplace.Contracts.V1.Requests;

/// <summary>Holds stock against a buyer.</summary>
public sealed record CreateReservationRequest
{
    /// <summary>How many units. One when omitted.</summary>
    public int? Quantity { get; init; }

    /// <summary>When the buyer can collect from.</summary>
    public DateTimeOffset? CollectFrom { get; init; }

    /// <summary>
    /// When the hold lapses. The deployment default applies when omitted, so an item is never held
    /// indefinitely by somebody who has gone quiet.
    /// </summary>
    public DateTimeOffset? CollectUntil { get; init; }

    /// <summary>Where. The parties' own words — "outside the station". Never a Location entity.</summary>
    public string? CollectionPlace { get; init; }

    /// <summary>What the buyer wants to say. Plain text.</summary>
    public string? Message { get; init; }
}
