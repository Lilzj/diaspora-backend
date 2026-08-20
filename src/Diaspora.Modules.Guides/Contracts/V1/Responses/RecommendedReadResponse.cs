namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// One curated next read, with the curator's reason.
/// <para>
/// <see cref="Reason"/> is what distinguishes a curated recommendation from a computed one, and it
/// is why the curated version is worth building first: "read this before you sign anything" is a
/// sentence a person wrote because they watched somebody get caught out, and no amount of
/// co-occurrence data produces it.
/// </para>
/// </summary>
public sealed record RecommendedReadResponse
{
    public required int Position { get; init; }

    /// <summary>Why this comes next. Rendered verbatim under the card.</summary>
    public string? Reason { get; init; }

    /// <summary>The guide, resolved. Recommendations pointing at a gone guide are omitted.</summary>
    public required GuideCardResponse Guide { get; init; }
}
