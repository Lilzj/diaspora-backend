using Diaspora.Modules.Recommendations.Domain.Candidates;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Preferences;
using Diaspora.Modules.Recommendations.Domain.Profiles;
using Diaspora.Modules.Recommendations.Extensibility.Caching;
using Diaspora.Modules.Recommendations.Extensibility.Search;
using Diaspora.Modules.Recommendations.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Recommendations.Infrastructure.Search;

/// <summary>
/// Turns a profile into boosts a search engine can consume.
/// <para>
/// <b>The whole search integration, and it deliberately returns no results.</b> Search owns matching;
/// this owns "which of the matches would this member prefer". A provider that ranked would be a
/// second search implementation, and the two would immediately disagree about what a query means.
/// </para>
/// <para>
/// <b>Boosts are multipliers around one, which is the shape a function-score query wants.</b> That
/// is not a coincidence — it is what makes the anticipated OpenSearch integration a mapping rather
/// than a redesign.
/// </para>
/// <para>
/// <b>An opted-out member gets nothing, and that is how a privacy preference reaches a surface this
/// module does not own.</b> Search asks, gets an empty list, and ranks by relevance alone.
/// </para>
/// <para>
/// <b>Trending is not personalised and is therefore cached across every member in a country.</b> A
/// trending row on a search screen is affordable precisely because it is the same for everybody.
/// </para>
/// </summary>
public sealed class ProfileSearchSignalProvider : ISearchSignalProvider
{
    private const string TrendingCachePrefix = "recommendations:trending:";

    /// <summary>How many interests are turned into boosts. Beyond a handful they stop discriminating.</summary>
    private const int MaximumInterestBoosts = 10;

    private readonly IPersonalizationProfileRepository _profiles;
    private readonly IPersonalizationPreferenceRepository _preferences;
    private readonly IRecommendationCandidateRepository _candidates;
    private readonly IRecommendationCache _cache;
    private readonly IClock _clock;
    private readonly IOptions<RecommendationOptions> _options;

    public ProfileSearchSignalProvider(
        IPersonalizationProfileRepository profiles,
        IPersonalizationPreferenceRepository preferences,
        IRecommendationCandidateRepository candidates,
        IRecommendationCache cache,
        IClock clock,
        IOptions<RecommendationOptions> options)
    {
        _profiles = profiles;
        _preferences = preferences;
        _candidates = candidates;
        _cache = cache;
        _clock = clock;
        _options = options;
    }

    public async Task<IReadOnlyList<SearchBoostSignal>> GetBoostSignalsAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var preference = await _preferences.FindAsync(userId, cancellationToken);

        if (preference is { IsPersonalizationEnabled: false })
        {
            return [];
        }

        var profile = await _profiles.FindAsync(userId, cancellationToken);

        if (profile is null)
        {
            return [];
        }

        var boosts = new List<SearchBoostSignal>();

        AddLocationBoosts(boosts, profile, preference);
        AddLanguageBoost(boosts, profile, preference);
        AddInterestBoosts(boosts, profile);

        return boosts;
    }

    public async Task<IReadOnlyList<SearchBoostSignal>> GetTrendingSignalsAsync(
        string? countryCode,
        RecommendationTargetType? targetType,
        CancellationToken cancellationToken = default)
    {
        var key = $"{TrendingCachePrefix}{countryCode ?? "global"}:{targetType?.ToString() ?? "all"}";

        return await _cache.GetOrCreateAsync(
            key,
            _options.Value.CacheLifetime,
            async token =>
            {
                var types = targetType is { } type ? new[] { type } : [];

                var popular = await _candidates.ListMostPopularAsync(
                    types, countryCode, MaximumInterestBoosts * 2, _clock.UtcNow, token);

                // Categories rather than individual items: a search engine boosts a field value, and
                // boosting twenty specific identifiers would be a hand-curated result list wearing a
                // trending label.
                return popular
                    .Where(candidate => candidate.CategoryKey is not null)
                    .GroupBy(candidate => candidate.CategoryKey!, StringComparer.Ordinal)
                    .OrderByDescending(group => group.Count())
                    .Take(MaximumInterestBoosts)
                    .Select(group => new SearchBoostSignal
                    {
                        Field = "category",
                        Value = group.Key,
                        Boost = 1.2m,
                        ReasonCode = RecommendationReasonCode.TrendingNow
                    })
                    .ToList();
            },
            cancellationToken);
    }

    /// <summary>
    /// Where the member is, as boosts.
    /// <para>
    /// Country strongest, then city, then neighbourhood — the reverse of the ranking strategy's
    /// ladder, and deliberately so. A search query already carries an intent; boosting a
    /// neighbourhood hard would bury a good national match under a mediocre local one, which is not
    /// what somebody who typed a query is asking for.
    /// </para>
    /// </summary>
    private static void AddLocationBoosts(
        List<SearchBoostSignal> boosts,
        PersonalizationProfile profile,
        PersonalizationPreference? preference)
    {
        var country = preference?.PreferredCountryCode ?? profile.PrimaryCountryCode;

        if (country is not null)
        {
            boosts.Add(new SearchBoostSignal
            {
                Field = "country",
                Value = country,
                Boost = 1.5m,
                ReasonCode = RecommendationReasonCode.LivesInCountry
            });
        }

        if (profile.PrimaryCityName is { } city)
        {
            boosts.Add(new SearchBoostSignal
            {
                Field = "city",
                Value = city,
                Boost = 1.3m,
                ReasonCode = RecommendationReasonCode.LivesInCity
            });
        }

        if (profile.PrimaryNeighborhoodName is { } neighborhood)
        {
            boosts.Add(new SearchBoostSignal
            {
                Field = "neighborhood",
                Value = neighborhood,
                Boost = 1.2m,
                ReasonCode = RecommendationReasonCode.LivesInNeighborhood
            });
        }
    }

    private static void AddLanguageBoost(
        List<SearchBoostSignal> boosts,
        PersonalizationProfile profile,
        PersonalizationPreference? preference)
    {
        var language = preference?.PreferredLanguage ?? profile.PreferredLanguage;

        if (language is null)
        {
            return;
        }

        boosts.Add(new SearchBoostSignal
        {
            Field = "language",
            Value = language,
            Boost = 1.4m,
            ReasonCode = RecommendationReasonCode.SpeaksLanguage
        });
    }

    /// <summary>
    /// The member's strongest interests, as boosts scaled by affinity.
    /// <para>
    /// The scaling keeps a weak interest from being asserted as strongly as a well-evidenced one: an
    /// affinity of 0.2 becomes a boost of 1.06, which nudges, and an affinity of 1 becomes 1.3,
    /// which decides.
    /// </para>
    /// </summary>
    private static void AddInterestBoosts(
        List<SearchBoostSignal> boosts, PersonalizationProfile profile)
    {
        foreach (var interest in profile.TopInterests(MaximumInterestBoosts))
        {
            var field = interest.Kind switch
            {
                InterestKind.Category => "category",
                InterestKind.Tag => "tag",
                InterestKind.Skill => "skill",
                InterestKind.Language => "language",
                InterestKind.SearchTerm => "term",
                _ => null
            };

            if (field is null)
            {
                continue;
            }

            boosts.Add(new SearchBoostSignal
            {
                Field = field,
                Value = interest.Key,
                Boost = decimal.Round(1m + (interest.Affinity * 0.3m), 4),
                ReasonCode = interest.IsExplicit
                    ? RecommendationReasonCode.MatchesCategory
                    : RecommendationReasonCode.MatchesInterests
            });
        }
    }
}
