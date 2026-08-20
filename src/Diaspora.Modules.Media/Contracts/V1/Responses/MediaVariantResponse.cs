namespace Diaspora.Modules.Media.Contracts.V1.Responses;

/// <summary>
/// One derived rendition of an asset.
/// <para>
/// <see cref="Url"/> is a route rather than a grant: it points at Media's content endpoint,
/// which authorises every request. For a private asset it carries a signed token and expires;
/// for a public one it does not, so a CDN in front of the API can cache it.
/// </para>
/// </summary>
public sealed record MediaVariantResponse
{
    /// <summary>
    /// <c>thumbnail</c>, <c>small</c>, <c>medium</c>, <c>large</c>, <c>preview</c>,
    /// <c>poster</c>, <c>transcoded</c>, <c>extractedText</c>. Camel-cased on the wire.
    /// </summary>
    public required string Purpose { get; init; }

    public required string Url { get; init; }

    public required string ContentType { get; init; }

    public required long SizeBytes { get; init; }

    public int? Width { get; init; }

    public int? Height { get; init; }
}
