namespace Diaspora.Modules.Media.Contracts.V1.Responses;

/// <summary>
/// A short-lived link to a private asset.
/// <para>
/// <see cref="ExpiresAt"/> is returned so a client can decide whether to re-request rather
/// than discovering expiry as a broken image. That matters most in the verification review
/// flow, where a reviewer may have a document open for longer than the link lives — ten
/// minutes, matching Trust's own <c>DocumentLinkFactory.LinkLifetime</c>.
/// </para>
/// </summary>
public sealed record SignedUrlResponse
{
    public required Guid MediaId { get; init; }

    public required string Url { get; init; }

    /// <summary>Which rendition the link opens. A link to one variant does not open another.</summary>
    public required string Variant { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }
}
