namespace Diaspora.Modules.Marketplace.Contracts.V1.Responses;

/// <summary>
/// A photograph, video or document, by reference.
/// <para>
/// <b>An identifier and a role, and no URL.</b> ADR-005 is the platform's rule and this is the
/// marketplace obeying it on the wire: the client asks Media for the asset and gets whatever
/// variant, signed URL and stripped metadata Media currently serves, rather than a link this
/// module captured when the listing was written.
/// </para>
/// </summary>
public sealed record ListingMediaResponse
{
    public required Guid MediaId { get; init; }

    /// <summary><c>Cover</c>, <c>Gallery</c>, <c>Video</c>, <c>Document</c> or <c>Manual</c>.</summary>
    public required string Role { get; init; }

    public required int Order { get; init; }

    /// <summary>The seller's caption. Distinct from Media's alt text.</summary>
    public string? Caption { get; init; }
}
