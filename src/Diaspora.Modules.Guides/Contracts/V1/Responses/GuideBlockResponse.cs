namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// One unit of content. The kind decides what the fields mean: prose in <see cref="Content"/>
/// for text kinds, a Media reference in <see cref="MediaId"/> for media kinds — the client
/// resolves it through Media's link endpoint, never from a stored URL.
/// </summary>
public sealed record GuideBlockResponse
{
    public required Guid BlockId { get; init; }

    /// <summary>A <c>GuideBlockKind</c> name.</summary>
    public required string Kind { get; init; }

    public required int Order { get; init; }

    /// <summary>The prose, or the caption for a media-kind block.</summary>
    public string? Content { get; init; }

    /// <summary>A Media identifier. Never a URL.</summary>
    public Guid? MediaId { get; init; }

    /// <summary>Opaque JSON for table and link payloads. The editor owns the shape.</summary>
    public string? Metadata { get; init; }
}
