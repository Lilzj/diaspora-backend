using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Policies;

namespace Diaspora.Modules.Marketplace.Domain.Listings;

/// <summary>
/// A photograph, video or document, by reference.
/// <para>
/// <b>An identifier and a role. No URL, no byte array, no content type, no dimensions.</b>
/// ADR-005 is the rule and this class is the whole of B10's compliance with it: Media owns the
/// bytes, the variants, the stripped EXIF and the signed URL that eventually renders, and a
/// listing that stored any of those would be holding a copy that goes stale the moment Media
/// re-processes the asset.
/// </para>
/// <para>
/// <b>The key is (listing, media, role), which makes attaching idempotent.</b> A client that
/// retries an attach after a timeout gets the same gallery, not two of the same photograph.
/// </para>
/// </summary>
public sealed class ListingMediaReference
{
    private ListingMediaReference(
        Guid mediaId, ListingMediaRole role, int order, string? caption)
    {
        MediaId = mediaId;
        Role = role;
        Order = order;
        Caption = caption;
    }

    private ListingMediaReference() { }

    /// <summary>B5's asset identifier. The only thing about the asset stored here.</summary>
    public Guid MediaId { get; private set; }

    public ListingMediaRole Role { get; private set; }

    /// <summary>Position within the role. What makes a gallery an ordered gallery.</summary>
    public int Order { get; private set; }

    /// <summary>
    /// The seller's caption. Distinct from Media's alt text, which describes the image for a
    /// screen reader and belongs to the asset rather than to this listing's use of it.
    /// </summary>
    public string? Caption { get; private set; }

    public static ListingMediaReference Create(
        Guid mediaId, ListingMediaRole role, int order, string? caption)
    {
        var trimmed = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim();

        if (trimmed is { Length: > MarketplaceLimits.MaxDisplayName })
        {
            trimmed = trimmed[..MarketplaceLimits.MaxDisplayName];
        }

        return new ListingMediaReference(mediaId, role, order, trimmed);
    }

    /// <summary>Moves the reference within its role, for a seller reordering a gallery.</summary>
    public ListingMediaReference WithOrder(int order) =>
        new(MediaId, Role, order, Caption);
}
