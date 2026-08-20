namespace Diaspora.Modules.Marketplace.Contracts.V1.Requests;

/// <summary>
/// Attaches a media asset to a listing, by reference.
/// <para>
/// The bytes were uploaded to Media first and this carries only the identifier it returned —
/// ADR-005. A request that carried a URL would be a listing storing a link that goes stale the
/// moment Media re-processes the asset.
/// </para>
/// </summary>
public sealed record AttachListingMediaRequest
{
    public required Guid MediaId { get; init; }

    /// <summary>
    /// <c>Cover</c>, <c>Gallery</c>, <c>Video</c>, <c>Document</c> or <c>Manual</c>. Gallery when
    /// omitted, which is what nearly every photograph is.
    /// </summary>
    public string? Role { get; init; }

    /// <summary>
    /// The seller's caption. Distinct from Media's alt text, which describes the image for a
    /// screen reader and belongs to the asset rather than to this listing's use of it.
    /// </summary>
    public string? Caption { get; init; }
}
