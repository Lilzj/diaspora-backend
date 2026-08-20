namespace Diaspora.Modules.Marketplace.Contracts.V1.Requests;

/// <summary>Publishes a listing.</summary>
public sealed record PublishListingRequest
{
    /// <summary>
    /// Whether it starts taking offers and reservations at once. False publishes it visible but
    /// shut — the shop that wants tomorrow's drop seen today.
    /// </summary>
    public bool ActivateImmediately { get; init; } = true;

    /// <summary>When it should stop being offered. The deployment default applies when omitted.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }
}
