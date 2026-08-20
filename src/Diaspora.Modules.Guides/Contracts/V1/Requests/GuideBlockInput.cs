namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// One content block as the editor submits it.
/// <para>
/// The kind decides what the fields mean: text kinds carry prose in <see cref="Content"/>;
/// media kinds carry the asset in <see cref="MediaId"/> — a reference, never a URL — and use
/// the content as a caption; Table and Links carry an opaque JSON payload in
/// <see cref="Metadata"/> that the editor owns.
/// </para>
/// </summary>
public sealed record GuideBlockInput
{
    public required Guid BlockId { get; init; }

    /// <summary>A <c>GuideBlockKind</c> name — <c>Markdown</c>, <c>Image</c>, <c>Callout</c>…</summary>
    public required string Kind { get; init; }

    public required int Order { get; init; }

    public string? Content { get; init; }

    /// <summary>The referenced asset, required for media-kind blocks.</summary>
    public Guid? MediaId { get; init; }

    /// <summary>Opaque JSON for table and link payloads.</summary>
    public string? Metadata { get; init; }
}
