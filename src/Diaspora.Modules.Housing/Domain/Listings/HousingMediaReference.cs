using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Policies;

namespace Diaspora.Modules.Housing.Domain.Listings;

/// <summary>
/// A photograph, floor plan, video or document, by reference.
/// <para>
/// <b>An identifier and a role. No URL, no byte array, no content type, no dimensions.</b>
/// ADR-005 is the rule and this class is the whole of B11's compliance with it: Media owns the
/// bytes, the variants, the stripped EXIF and the signed URL that eventually renders, and a
/// listing that stored any of those would be holding a copy that goes stale the moment Media
/// re-processes the asset.
/// </para>
/// <para>
/// <b>Stripped EXIF is not a detail here.</b> A photograph taken inside a flat carries the
/// coordinates of that flat, and a housing platform that published originals would defeat its own
/// address privacy with the first gallery image. B5 strips it; B11 relies on that and stores no
/// image data of its own.
/// </para>
/// <para>
/// <b>The key is (listing, media, role), which makes attaching idempotent.</b> A client that
/// retries an attach after a timeout gets the same gallery, not two of the same photograph.
/// </para>
/// </summary>
public sealed class HousingMediaReference
{
    private HousingMediaReference(
        Guid mediaId, HousingMediaRole role, int order, string? caption)
    {
        MediaId = mediaId;
        Role = role;
        Order = order;
        Caption = caption;
    }

    private HousingMediaReference() { }

    /// <summary>B5's asset identifier. The only thing about the asset stored here.</summary>
    public Guid MediaId { get; private set; }

    public HousingMediaRole Role { get; private set; }

    /// <summary>Position within the role. What makes a gallery an ordered gallery.</summary>
    public int Order { get; private set; }

    /// <summary>
    /// The landlord's caption - "kitchen, looking north". Distinct from Media's alt text, which
    /// describes the image for a screen reader and belongs to the asset rather than to this
    /// listing's use of it.
    /// </summary>
    public string? Caption { get; private set; }

    /// <summary>
    /// Whether this reference is a document rather than something to render in a gallery.
    /// Documents are disclosed on the same terms as an address, not shown to browsers.
    /// </summary>
    public bool IsDocument =>
        Role is HousingMediaRole.Document or HousingMediaRole.LeaseDocument;

    public static HousingMediaReference Create(
        Guid mediaId, HousingMediaRole role, int order, string? caption)
    {
        var trimmed = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim();

        if (trimmed is { Length: > HousingLimits.MaxDisplayName })
        {
            trimmed = trimmed[..HousingLimits.MaxDisplayName];
        }

        return new HousingMediaReference(mediaId, role, order, trimmed);
    }

    /// <summary>Moves the reference within its role, for a landlord reordering a gallery.</summary>
    public HousingMediaReference WithOrder(int order) => new(MediaId, Role, order, Caption);
}
