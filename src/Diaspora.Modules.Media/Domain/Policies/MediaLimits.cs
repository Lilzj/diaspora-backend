using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.ValueObjects;

namespace Diaspora.Modules.Media.Domain.Policies;

/// <summary>
/// The ceilings the platform enforces, per media kind.
/// <para>
/// Two of these numbers are not choices. The verification-document ceiling is <b>5 MB</b>
/// because the client already tells members so, on screen, in two places
/// (<c>verification/country.tsx:276</c> and <c>verification/diaspora.tsx:498</c>: "Supports
/// PDF, PNG, JPG (Max 5MB)"), and Trust's own <c>VerificationDocument.MaxSizeInBytes</c> is
/// the same value. A backend that quietly enforced a different number would be contradicting
/// its own UI.
/// </para>
/// <para>
/// The rest are chosen from what the flows actually carry. An image ceiling of 15 MB clears
/// a 48-megapixel phone photograph with room to spare; a video ceiling of 512 MB is about ten
/// minutes at the 720p bitrate the transcode targets, which is far more than a listing walk-
/// through needs and far less than an unbounded upload costs.
/// </para>
/// <para>
/// Defaults, not policy. A deployment overrides any of them through
/// <c>MediaUploadOptions</c>; this class is what the domain falls back to, so the invariant
/// holds even when configuration is absent.
/// </para>
/// </summary>
public static class MediaLimits
{
    /// <summary>
    /// The largest upload that may skip the session protocol and arrive in one request.
    /// <para>
    /// Above this, a failure means re-sending everything, which on a mobile connection means
    /// members who never successfully upload at all. Below it, a session is three round trips
    /// where one would do.
    /// </para>
    /// </summary>
    public static readonly ByteSize DirectUploadCeiling = ByteSize.FromMegabytes(8);

    /// <summary>The size of one part in a chunked upload.</summary>
    public static readonly ByteSize ChunkSize = ByteSize.FromMegabytes(5);

    /// <summary>
    /// The smallest acceptable non-final chunk. Without a floor, a client can open a session
    /// and send a million one-byte parts, and every one of them is a database row.
    /// </summary>
    public static readonly ByteSize MinimumChunkSize = ByteSize.FromKilobytes(64);

    /// <summary>Parts one session may hold. At 5 MB each this bounds a session at 50 GB.</summary>
    public const int MaxChunksPerSession = 10_000;

    /// <summary>
    /// How long an unfinished upload survives. Long enough that a member can finish a wizard
    /// the next morning — every composer in the app persists a draft and expects to resume —
    /// short enough that abandoned parts are not permanent storage.
    /// </summary>
    public static readonly TimeSpan UploadSessionLifetime = TimeSpan.FromHours(24);

    /// <summary>How long a minted signed URL stays valid.</summary>
    public static readonly TimeSpan SignedUrlLifetime = TimeSpan.FromMinutes(15);

    /// <summary>
    /// The ceiling for a link to a verification document. Deliberately shorter than the
    /// general one and matched to Trust's own <c>DocumentLinkFactory.LinkLifetime</c>: these
    /// are immigration documents, and a link that outlives the page it was rendered on is a
    /// link that ends up pasted into a support ticket.
    /// </summary>
    public static readonly TimeSpan SensitiveSignedUrlLifetime = TimeSpan.FromMinutes(10);

    /// <summary>The largest upload accepted for a given kind.</summary>
    public static ByteSize MaxSizeFor(MediaKind kind) => kind switch
    {
        MediaKind.Image => ByteSize.FromMegabytes(15),
        MediaKind.Video => ByteSize.FromMegabytes(512),
        MediaKind.Audio => ByteSize.FromMegabytes(50),
        MediaKind.Document => ByteSize.FromMegabytes(25),
        MediaKind.Archive => ByteSize.FromMegabytes(100),

        // The client's own published number. Changing it means changing the UI first.
        MediaKind.VerificationDocument => ByteSize.FromMegabytes(5),

        MediaKind.AiAsset => ByteSize.FromMegabytes(25),
        MediaKind.Model3D => ByteSize.FromMegabytes(100),
        _ => ByteSize.FromMegabytes(5)
    };

    /// <summary>
    /// How many assets a collection of this kind may hold.
    /// <para>
    /// The two specific numbers come from the client, not from taste: the provider wizard
    /// renders exactly four slots ("Add up to 4 high-quality photos"), and the message
    /// attachment picker sets <c>selectionLimit: 5</c>.
    /// </para>
    /// </summary>
    public static int MaxItemsFor(MediaCollectionKind kind) => kind switch
    {
        MediaCollectionKind.ProviderGallery => 4,
        MediaCollectionKind.MessageAttachments => 5,
        MediaCollectionKind.Gallery => 30,
        MediaCollectionKind.VerificationEvidence => 10,
        MediaCollectionKind.ArticleImages => 50,
        MediaCollectionKind.LocationPhotos => 30,
        _ => 50
    };

    /// <summary>
    /// Whether a kind may ever be made public.
    /// <para>
    /// Verification documents may not, at any tier, by any grant. That is not a default a
    /// caller can override — it is the reason Trust was willing to hand its file storage to
    /// another module.
    /// </para>
    /// </summary>
    public static bool CanBePublic(MediaKind kind) => kind is not MediaKind.VerificationDocument;

    /// <summary>The visibility an asset of this kind is created with.</summary>
    public static MediaVisibility DefaultVisibilityFor(MediaKind kind) => kind switch
    {
        MediaKind.VerificationDocument => MediaVisibility.Private,
        MediaKind.Document => MediaVisibility.Private,
        MediaKind.Archive => MediaVisibility.Private,
        _ => MediaVisibility.Private
    };

    /// <summary>Which size ladder applies to a kind. Empty for anything not renderable.</summary>
    public static IReadOnlyList<MediaVariantPurpose> LadderFor(MediaKind kind) => kind switch
    {
        MediaKind.Image =>
        [
            MediaVariantPurpose.Thumbnail,
            MediaVariantPurpose.Small,
            MediaVariantPurpose.Medium,
            MediaVariantPurpose.Large,
            MediaVariantPurpose.Preview
        ],

        MediaKind.Video => [MediaVariantPurpose.Poster, MediaVariantPurpose.Transcoded],

        _ => []
    };
}
