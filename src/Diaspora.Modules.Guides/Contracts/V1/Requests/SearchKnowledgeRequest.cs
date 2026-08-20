namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// Every filter the knowledge search endpoint offers, bound from the query string.
/// <para>
/// <b>Every value type here is nullable, and that is a rule the platform paid to learn twice.</b> A
/// non-nullable value type on an <c>[AsParameters]</c> request is a <em>required</em> query
/// parameter: omit it and ASP.NET Core answers 400 with an empty body — no validation message,
/// nothing above a debug line in the logs, and a client with no way to work out what it left out.
/// B9 recorded the shape with a comment explaining it, B11 repeated it anyway, and an architecture
/// test now fails the build for it. A search link with no parameters at all must return the first
/// page of everything.
/// </para>
/// <para>
/// Filters travel in the query string and never in a body: a GET with a body is unusable from a
/// browser, uncacheable by any intermediary and unlinkable — and a search result that cannot be
/// shared as a URL is the one thing a knowledge base most needs to be shareable, because sharing is
/// how the answer reaches the person who needed it.
/// </para>
/// </summary>
public sealed record SearchKnowledgeRequest
{
    /// <summary>The phrase. Absent browses rather than searches, which is a valid thing to ask.</summary>
    public string? Q { get; init; }

    /// <summary>ISO 3166-1 alpha-2. Case-insensitive on the way in.</summary>
    public string? CountryCode { get; init; }

    /// <summary>A navigation row key. Expanded server-side into its source guide categories.</summary>
    public string? CategoryKey { get; init; }

    /// <summary>An ISO 639-1 code.</summary>
    public string? Language { get; init; }

    /// <summary>A tag slug.</summary>
    public string? Tag { get; init; }

    /// <summary><c>beginner</c>, <c>intermediate</c> or <c>advanced</c>.</summary>
    public string? Difficulty { get; init; }

    /// <summary>The longest read the caller will accept, in minutes.</summary>
    public int? MaxReadingTime { get; init; }

    /// <summary>Trust-verified guides only.</summary>
    public bool? VerifiedOnly { get; init; }

    /// <summary>Guides carrying an authority badge. Narrower than verified.</summary>
    public bool? OfficialOnly { get; init; }

    /// <summary>Guides a curator placed on this country's shelf.</summary>
    public bool? FeaturedOnly { get; init; }

    /// <summary><c>relevance</c>, <c>newest</c>, <c>most-helpful</c>, <c>popular</c>, <c>recently-updated</c>.</summary>
    public string? Sort { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
