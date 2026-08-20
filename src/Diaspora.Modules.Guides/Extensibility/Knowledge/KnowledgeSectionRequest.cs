namespace Diaspora.Modules.Guides.Extensibility.Knowledge;

/// <summary>
/// Everything a section provider is told about the shelf it is being asked to fill.
/// <para>
/// <b>A resolved request, not a query.</b> The category has already been looked up, its source
/// guide categories already expanded, the curated shelf already read. A provider composes a filter
/// from these fields and asks the guide repository — it never resolves a knowledge category, never
/// reads a directory and never composes an expression tree of its own, which is the same discipline
/// B8 applies to its ranking strategies and for the same reason: a contributed provider that could
/// write its own predicate would eventually write one EF fails to translate, inside somebody else's
/// stack trace.
/// </para>
/// </summary>
public sealed record KnowledgeSectionRequest
{
    /// <summary>Upper-case ISO 3166-1 alpha-2. Always present — sections are country-scoped.</summary>
    public required string CountryCode { get; init; }

    /// <summary>The knowledge category being viewed, or null on the country's own overview.</summary>
    public string? KnowledgeCategoryKey { get; init; }

    /// <summary>
    /// The guide category keys the shelf draws from, already expanded from the category's sources.
    /// Empty on a country-wide shelf, which means every category.
    /// </summary>
    public IReadOnlyList<string> GuideCategoryKeys { get; init; } = [];

    /// <summary>The reader's guide language filter, when they set one.</summary>
    public string? Language { get; init; }

    /// <summary>The reader's locale, for anything a provider labels.</summary>
    public string? Locale { get; init; }

    /// <summary>How many guides the shelf holds. Already clamped.</summary>
    public required int Limit { get; init; }

    /// <summary>
    /// The guides a curator explicitly placed on this shelf, in curated order.
    /// <para>
    /// Given to every provider, not only the featured one, because a provider that ranks by
    /// popularity should still be able to keep a curated guide off its own list rather than showing
    /// it twice on one screen.
    /// </para>
    /// </summary>
    public IReadOnlyList<Guid> CuratedGuideIds { get; init; } = [];

    /// <summary>Now, supplied rather than read, so a provider is deterministic under test.</summary>
    public required DateTimeOffset At { get; init; }
}
