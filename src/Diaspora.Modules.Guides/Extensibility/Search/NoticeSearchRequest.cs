namespace Diaspora.Modules.Guides.Extensibility.Search;

/// <summary>
/// Everything an official notice search can ask for, in terms no engine implementation can refuse to
/// understand.
/// <para>
/// <b>A third search request on this module, and the three search for genuinely different
/// things.</b> A guide search finds documents somebody wrote. A knowledge search finds documents
/// within a country's organised knowledge. This finds statements an authority made — which means it
/// filters by who issued it, how serious it is, and when it was true, and none of those fields exists
/// on either of the others. Merging them would produce a request record where two thirds of the
/// fields are meaningless on any given call.
/// </para>
/// <para>
/// <b>Primitives and enumerable filters only, deliberately.</b> Nothing here is an expression, a
/// delegate or a domain object — which is what lets a future OpenSearch adapter satisfy this contract
/// by translating fields into a query DSL. The architecture suite fails the build on any field that
/// could only be answered in process. Dates cross as ISO-8601 strings for the same reason: a query
/// DSL is JSON, and a <c>DateTimeOffset</c> is the one common type whose serialisation two engines
/// will disagree about.
/// </para>
/// </summary>
public sealed record NoticeSearchRequest
{
    /// <summary>The phrase. Null browses rather than searches, which is a valid thing to ask for.</summary>
    public string? Text { get; init; }

    /// <summary>Upper-case ISO 3166-1 alpha-2, already normalized.</summary>
    public string? CountryCode { get; init; }

    /// <summary>The notice category keys to restrict to. Empty means every category.</summary>
    public IReadOnlyList<string> CategoryKeys { get; init; } = [];

    /// <summary>The issuing authority, as a bare identifier string. Null means any.</summary>
    public string? AuthorityId { get; init; }

    /// <summary>
    /// Severity names to restrict to — <c>critical</c>, <c>high</c>. Empty means every severity.
    /// <para>
    /// Names rather than the enum, because this record crosses into an index that has never heard of
    /// a .NET type. An engine adapter maps them to whatever its own schema stores.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> Severities { get; init; } = [];

    /// <summary>A normalized BCP-47 tag the notice was officially issued in.</summary>
    public string? Language { get; init; }

    /// <summary>Only notices from an authority Trust has confirmed.</summary>
    public bool OfficialOnly { get; init; }

    /// <summary>
    /// Only notices that are guidance right now — published, in effect, not expired, not superseded.
    /// </summary>
    public bool ActiveOnly { get; init; }

    /// <summary>Only notices whose expiry has passed. The editorial cleanup queue.</summary>
    public bool ExpiredOnly { get; init; }

    /// <summary>ISO-8601. Only notices published on or after this instant.</summary>
    public string? PublishedFrom { get; init; }

    /// <summary>ISO-8601. Only notices taking effect on or after this instant.</summary>
    public string? EffectiveFrom { get; init; }

    /// <summary>
    /// A sort key — <c>relevance</c>, <c>priority</c>, <c>newest</c>, <c>oldest</c>,
    /// <c>effective-soonest</c>, <c>expiring-soonest</c>, <c>most-viewed</c>.
    /// </summary>
    public string? Sort { get; init; }

    public string? Cursor { get; init; }

    /// <summary>Already clamped by the caller.</summary>
    public required int Limit { get; init; }
}
