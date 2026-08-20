namespace Diaspora.Modules.Guides.Extensibility.Search;

/// <summary>
/// Everything a search engine needs about one guide, denormalised into one flat record.
/// <para>
/// Heavy denormalisation on purpose: a search index cannot join, and reindexing one document
/// must not require loading six aggregates. The body text is the concatenated prose — captions,
/// table payloads and code excluded, for the same reason the reading time excludes them.
/// </para>
/// </summary>
public sealed record GuideSearchDocument
{
    public required Guid GuideId { get; init; }

    public required string Slug { get; init; }

    public required string Title { get; init; }

    public string? Summary { get; init; }

    /// <summary>The concatenated prose of every text block, in reading order.</summary>
    public required string BodyText { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public required string CategoryKey { get; init; }

    public required string CountryCode { get; init; }

    public string? RegionName { get; init; }

    public string? CityName { get; init; }

    /// <summary>Lower-case ISO 639-1 — also the analyzer hint for a real engine.</summary>
    public required string Language { get; init; }

    public required Guid AuthorId { get; init; }

    public required bool IsVerified { get; init; }

    public required bool IsFeatured { get; init; }

    /// <summary>A pre-computed engagement score, so ranking needs no second lookup.</summary>
    public required double PopularityScore { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }
}
