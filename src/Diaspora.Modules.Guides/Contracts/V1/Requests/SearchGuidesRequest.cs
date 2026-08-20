namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// Everything the discovery endpoint accepts, bound from the query string.
/// <para>
/// Every optional field is nullable, including the flags — minimal-API binding treats a
/// non-nullable value type bound from the query string as <em>required</em> and answers an
/// absent one with an empty 400. Null means "not asked for".
/// </para>
/// <para>
/// The text term binds from either <see cref="Query"/> or <see cref="Q"/>, because both
/// spellings exist in the wild (the B4 precedent); the handler coalesces them.
/// </para>
/// </summary>
public sealed record SearchGuidesRequest
{
    /// <summary>Free text over title, summary and tags.</summary>
    public string? Query { get; init; }

    /// <summary>The short spelling of <see cref="Query"/>. Same meaning; first non-empty wins.</summary>
    public string? Q { get; init; }

    /// <summary>Upper-case ISO 3166-1 alpha-2.</summary>
    public string? Country { get; init; }

    public string? Region { get; init; }

    public string? City { get; init; }

    /// <summary>A category key from the catalogue.</summary>
    public string? Category { get; init; }

    /// <summary>A normalized tag slug.</summary>
    public string? Tag { get; init; }

    /// <summary>Lower-case ISO 639-1.</summary>
    public string? Language { get; init; }

    public Guid? AuthorId { get; init; }

    /// <summary>The client's "verified guides only" toggle.</summary>
    public bool? Verified { get; init; }

    public bool? Featured { get; init; }

    /// <summary>A <c>GuideDifficulty</c> name.</summary>
    public string? Difficulty { get; init; }

    /// <summary>A sort key — <c>newest</c>, <c>most-helpful</c>, <c>recently-updated</c>, <c>popular</c>, <c>trending</c>.</summary>
    public string? Sort { get; init; }

    /// <summary>Opaque. From a previous page's <c>nextCursor</c>.</summary>
    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
