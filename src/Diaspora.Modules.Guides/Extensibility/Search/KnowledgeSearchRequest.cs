namespace Diaspora.Modules.Guides.Extensibility.Search;

/// <summary>
/// Everything a knowledge search can ask for, in terms no engine implementation can refuse to
/// understand.
/// <para>
/// <b>Distinct from B8's guide search request, and the difference is what the two are searching
/// for.</b> A guide search finds documents; a knowledge search finds documents <em>within a
/// country's organised knowledge</em> — which means it filters by knowledge category rather than
/// filing category, understands "official" as a distinct thing from "verified", and knows that a
/// reading-time ceiling is a real user intent ("what can I read before my appointment").
/// </para>
/// <para>
/// <b>Primitives and enumerable filters only, deliberately.</b> Nothing here is an expression, a
/// delegate or a domain object. That is what lets a future OpenSearch adapter satisfy this contract
/// by translating fields into a query DSL — the promise the brief makes when it says future search
/// integration must be an infrastructure change. An engine that received a predicate could only
/// ever be an in-process one.
/// </para>
/// </summary>
public sealed record KnowledgeSearchRequest
{
    /// <summary>The phrase. Null browses rather than searches, which is a valid thing to ask for.</summary>
    public string? Text { get; init; }

    /// <summary>Upper-case ISO 3166-1 alpha-2, already normalized.</summary>
    public string? CountryCode { get; init; }

    /// <summary>
    /// The knowledge category being searched within, for the engine's own diagnostics and for a
    /// future engine that boosts by navigation context. The keys it actually filters on are below.
    /// </summary>
    public string? KnowledgeCategoryKey { get; init; }

    /// <summary>
    /// The guide category keys to restrict to, already expanded from the knowledge category's
    /// sources. Empty means every category.
    /// </summary>
    public IReadOnlyList<string> GuideCategoryKeys { get; init; } = [];

    /// <summary>A lower-case ISO 639-1 code.</summary>
    public string? Language { get; init; }

    /// <summary>A normalized tag slug.</summary>
    public string? Tag { get; init; }

    /// <summary>A difficulty name — <c>beginner</c>, <c>intermediate</c>, <c>advanced</c>.</summary>
    public string? Difficulty { get; init; }

    /// <summary>
    /// The longest read the caller will accept, in minutes.
    /// <para>
    /// A real intent rather than a nicety: somebody standing in a queue at an immigration office
    /// has five minutes, and a forty-minute guide is the wrong answer however relevant it is.
    /// </para>
    /// </summary>
    public int? MaxReadingTimeMinutes { get; init; }

    /// <summary>Trust-verified guides only.</summary>
    public bool VerifiedOnly { get; init; }

    /// <summary>
    /// Official guides only — verified <em>and</em> carrying an authority badge from Trust.
    /// <para>
    /// Narrower than verified, and the distinction matters to a reader deciding whether to act on
    /// something: a verified guide has been checked, an official one came from the body that makes
    /// the rule.
    /// </para>
    /// </summary>
    public bool OfficialOnly { get; init; }

    /// <summary>Guides a curator placed on this country's shelf.</summary>
    public bool FeaturedOnly { get; init; }

    /// <summary>
    /// Identifiers to restrict the answer to, when the caller has already curated a set.
    /// Empty means no restriction.
    /// </summary>
    public IReadOnlyList<Guid> GuideIds { get; init; } = [];

    /// <summary>A sort key — <c>relevance</c>, <c>newest</c>, <c>most-helpful</c>, <c>popular</c>, <c>recently-updated</c>.</summary>
    public string? Sort { get; init; }

    public string? Cursor { get; init; }

    /// <summary>Already clamped by the caller.</summary>
    public required int Limit { get; init; }
}
