using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Marketplace.V1.Events;

/// <summary>
/// A media asset was attached to a listing, by reference.
/// <para>
/// <b>An identifier and a role. No URL, ever.</b> ADR-005 is the platform's rule and this contract
/// is the marketplace's compliance with it travelling outward: a consumer that wants to render the
/// photograph asks Media for it, and gets whatever variant, signed URL and stripped metadata Media
/// currently serves — rather than a link this module captured months ago.
/// </para>
/// </summary>
public sealed record ListingMediaAttachedIntegrationEvent : IntegrationEvent
{
    public required Guid ListingId { get; init; }

    public required Guid SellerId { get; init; }

    public required Guid MediaId { get; init; }

    /// <summary><c>Cover</c>, <c>Gallery</c>, <c>Video</c>, <c>Document</c>, <c>Manual</c>.</summary>
    public required string Role { get; init; }

    public required DateTimeOffset AttachedAt { get; init; }
}
