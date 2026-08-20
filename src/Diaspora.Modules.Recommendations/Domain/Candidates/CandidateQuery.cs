using Diaspora.Modules.Recommendations.Domain.Enums;

namespace Diaspora.Modules.Recommendations.Domain.Candidates;

/// <summary>
/// What the engine asks the candidate store for.
/// <para>
/// <b>Deliberately coarse, and that is the division of labour the module depends on.</b> This
/// narrows a table of everything recommendable down to a pool the ranking pass can afford to score
/// — country, type, freshness, activity. It does not rank, does not personalise and does not apply
/// policy, because a store that could do those things would be a second ranking implementation that
/// nobody could explain.
/// </para>
/// <para>
/// The consequence is that the pool is intentionally larger than the page by a wide margin: policies
/// exclude and diversity demotes after this runs, and a pool sized to the page would come out of
/// both at three items.
/// </para>
/// </summary>
public sealed record CandidateQuery
{
    /// <summary>Empty means every type — the home feed's case.</summary>
    public IReadOnlyList<RecommendationTargetType> TargetTypes { get; init; } = [];

    /// <summary>ISO 3166-1 alpha-2, upper case. Null widens to every country.</summary>
    public string? CountryCode { get; init; }

    public string? CityName { get; init; }

    public string? NeighborhoodName { get; init; }

    /// <summary>
    /// Whether to include candidates with no country at all.
    /// <para>
    /// True by default and load-bearing: a guide about visa applications has no country, and a
    /// country-filtered pool that excluded it would leave the guides surface empty for everybody.
    /// </para>
    /// </summary>
    public bool IncludeGlobal { get; init; } = true;

    /// <summary>Restricts to a language. Null accepts every language including none.</summary>
    public string? LanguageCode { get; init; }

    /// <summary>Restricts to these categories. Empty accepts every category.</summary>
    public IReadOnlyList<string> CategoryKeys { get; init; } = [];

    /// <summary>Nothing published before this. Null accepts anything.</summary>
    public DateTimeOffset? PublishedAfter { get; init; }

    /// <summary>The trust standing the member has. Candidates asking for more are left out.</summary>
    public int MemberTrustLevel { get; init; }

    public bool MemberIsVerified { get; init; }

    /// <summary>Only candidates the owning module marked verified.</summary>
    public bool VerifiedOnly { get; init; }

    /// <summary>Excludes targets the member has already engaged with or dismissed.</summary>
    public IReadOnlyList<Guid> ExcludedTargetIds { get; init; } = [];

    /// <summary>How many rows to pull. The engine's pool size.</summary>
    public int Limit { get; init; } = 400;

    /// <summary>The moment the query is evaluated against. Never <c>UtcNow</c> inside the store.</summary>
    public DateTimeOffset AsOf { get; init; }
}
