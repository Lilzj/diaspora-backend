namespace Diaspora.Modules.Guides.Integration;

/// <summary>
/// One guide, described in terms every consuming module can read.
/// <para>
/// <b>Flat primitives on purpose.</b> It crosses a module boundary, and a consumer that had to
/// reference B8's value objects to read a country code would be coupled to this module's
/// internals rather than to its contract — the same reasoning B4 and B7 record for their own
/// summaries. A Community Update that wants to link "the bank account guide ✓" needs a title, a
/// slug, a verified flag and a hero reference, and nothing else.
/// </para>
/// <para>
/// <b>Media is a <see cref="Guid"/>, not a URL</b>, per ADR-005. The consumer resolves it
/// through Media's link endpoint exactly as it resolves its own.
/// </para>
/// </summary>
public sealed record GuideSummary
{
    public required Guid GuideId { get; init; }

    /// <summary>The deep-link handle. Stable across retitles.</summary>
    public required string Slug { get; init; }

    public required string Title { get; init; }

    public string? Summary { get; init; }

    public required string CategoryKey { get; init; }

    /// <summary>Upper-case ISO 3166-1 alpha-2 — the jurisdiction the knowledge applies to.</summary>
    public required string CountryCode { get; init; }

    /// <summary>Lower-case ISO 639-1.</summary>
    public required string Language { get; init; }

    public required Guid AuthorId { get; init; }

    /// <summary>The rolled-up trust answer. What a badge renders from.</summary>
    public required bool IsVerified { get; init; }

    public required bool IsFeatured { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    /// <summary>A Media identifier for the hero image. Never a URL.</summary>
    public Guid? HeroMediaId { get; init; }
}
