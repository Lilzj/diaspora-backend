using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Ranking;

namespace Diaspora.Modules.Recommendations.Infrastructure.Ranking;

/// <summary>
/// How close a candidate is to where the member actually is.
/// <para>
/// <b>The strongest signal the platform has about a diaspora member, and the one it can rely on
/// from the first minute.</b> Somebody who registered ninety seconds ago has a country, and a
/// country narrows the platform's whole corpus to the part that could possibly be useful to them.
/// This is why it runs on the cold-start path and why the cold-start ladder is built on it.
/// </para>
/// <para>
/// <b>Three grains, scored as a ladder rather than as a distance.</b> Neighbourhood beats city beats
/// country, and a geographic distance would be both unavailable — this module holds no coordinates,
/// deliberately — and wrong: two neighbourhoods a kilometre apart across a border are not near each
/// other in any sense a member cares about.
/// </para>
/// <para>
/// <b>A candidate with no country scores the global floor rather than zero, and that is
/// load-bearing.</b> A guide about visa applications belongs to nowhere; scoring it zero would rank
/// the platform's most broadly useful content below a local listing on every location-weighted
/// surface. The floor says "relevant everywhere, specific nowhere", which is exactly what it is.
/// </para>
/// <para>
/// <b>Derived affinities count as well as the primary location.</b> A member in Port Louis who reads
/// Lagos content every week has a Lagos affinity, and content from Lagos should not score zero for
/// them. This is what makes the strategy work for the platform's actual members, most of whom care
/// about more than one place.
/// </para>
/// </summary>
public sealed class LocationRankingStrategy : IRankingStrategy
{
    public const string StrategyKey = "location";

    /// <summary>What a candidate belonging to nowhere scores. See the class summary.</summary>
    public const string GlobalFloorParameter = "globalfloor";

    private const decimal DefaultGlobalFloor = 0.35m;

    private const decimal NeighborhoodScore = 1.0m;
    private const decimal CityScore = 0.8m;
    private const decimal CountryScore = 0.55m;

    /// <summary>How much a derived affinity is worth relative to the member's actual location.</summary>
    private const decimal AffinityDiscount = 0.7m;

    public string Key => StrategyKey;

    public RankingStrategyKind Kind => RankingStrategyKind.Location;

    public bool SupportsColdStart => true;

    public Task<IReadOnlyList<RankingScore>> RankAsync(
        RankingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var personalization = context.Personalization;
        var profile = personalization.Profile;
        var globalFloor = context.Configuration.ParameterOrDefault(GlobalFloorParameter, DefaultGlobalFloor);

        var country = personalization.EffectiveCountryCode;
        var city = profile.PrimaryCityName;
        var neighborhood = profile.PrimaryNeighborhoodName;

        var scores = new List<RankingScore>(context.Candidates.Count);

        foreach (var candidate in context.Candidates)
        {
            if (candidate.CountryCode is null && candidate.CityName is null)
            {
                scores.Add(new RankingScore
                {
                    Value = globalFloor,
                    ReasonCode = RecommendationReasonCode.MatchesInterests,
                    SignalKinds = []
                });

                continue;
            }

            if (Matches(candidate.NeighborhoodName, neighborhood))
            {
                scores.Add(Score(NeighborhoodScore, RecommendationReasonCode.LivesInNeighborhood, neighborhood));
                continue;
            }

            if (Matches(candidate.CityName, city))
            {
                scores.Add(Score(CityScore, RecommendationReasonCode.LivesInCity, city));
                continue;
            }

            if (Matches(candidate.CountryCode, country))
            {
                scores.Add(Score(CountryScore, RecommendationReasonCode.LivesInCountry, candidate.CountryCode));
                continue;
            }

            // Nowhere the member is, but somewhere they keep looking. See the class summary.
            var affinity = BestAffinity(context, candidate.CountryCode, candidate.CityName, candidate.NeighborhoodName);

            if (affinity.Value > 0m)
            {
                scores.Add(new RankingScore
                {
                    Value = decimal.Round(affinity.Value * AffinityDiscount, 6),
                    ReasonCode = affinity.IsStated
                        ? RecommendationReasonCode.MovingToCountry
                        : RecommendationReasonCode.NearYou,
                    ReasonDetail = affinity.Detail,
                    SignalKinds = [SignalKind.Location, SignalKind.Viewed]
                });

                continue;
            }

            scores.Add(RankingScore.None());
        }

        return Task.FromResult<IReadOnlyList<RankingScore>>(scores);
    }

    private static RankingScore Score(decimal value, RecommendationReasonCode reason, string? detail) =>
        new()
        {
            Value = value,
            ReasonCode = reason,
            ReasonDetail = detail,
            SignalKinds = [SignalKind.Location]
        };

    private static (decimal Value, string? Detail, bool IsStated) BestAffinity(
        RankingContext context, string? countryCode, string? cityName, string? neighborhoodName)
    {
        var profile = context.Personalization.Profile;
        var preference = context.Personalization.Preference;

        var best = 0m;
        string? detail = null;
        var stated = false;

        foreach (var (scope, key) in Candidates(countryCode, cityName, neighborhoodName))
        {
            var affinity = profile.LocationAffinityFor(scope, key);

            if (affinity > best)
            {
                best = affinity;
                detail = key;
            }

            // A stated favourite outranks any derived affinity: somebody planning a move has no
            // history in the place they are moving to, which is the whole reason they stated it.
            var isFavorite = preference?.FavoriteLocations.Any(location =>
                location.Scope == scope &&
                string.Equals(location.Key, Normalize(scope, key), StringComparison.OrdinalIgnoreCase)) == true;

            if (isFavorite && best < 1m)
            {
                best = 1m;
                detail = key;
                stated = true;
            }
        }

        return (best, detail, stated);
    }

    private static IEnumerable<(AffinityScope Scope, string Key)> Candidates(
        string? countryCode, string? cityName, string? neighborhoodName)
    {
        if (!string.IsNullOrWhiteSpace(neighborhoodName))
        {
            yield return (AffinityScope.Neighborhood, neighborhoodName);
        }

        if (!string.IsNullOrWhiteSpace(cityName))
        {
            yield return (AffinityScope.City, cityName);
        }

        if (!string.IsNullOrWhiteSpace(countryCode))
        {
            yield return (AffinityScope.Country, countryCode);
        }
    }

    private static string Normalize(AffinityScope scope, string key) =>
        scope == AffinityScope.Country ? key.Trim().ToUpperInvariant() : key.Trim().ToLowerInvariant();

    private static bool Matches(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) &&
        !string.IsNullOrWhiteSpace(right) &&
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
