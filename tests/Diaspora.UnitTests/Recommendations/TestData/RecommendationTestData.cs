using Diaspora.Contracts.Authorization;
using Diaspora.Modules.Recommendations.Domain.Candidates;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.Modules.Recommendations.Domain.Preferences;
using Diaspora.Modules.Recommendations.Domain.Profiles;
using Diaspora.Modules.Recommendations.Domain.Strategies;
using Diaspora.Modules.Recommendations.Domain.ValueObjects;
using Diaspora.Modules.Recommendations.Extensibility.Personalization;
using Diaspora.Modules.Recommendations.Extensibility.Ranking;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.UnitTests.Recommendations.TestData;

/// <summary>
/// Builders for the recommendation suite.
/// <para>
/// Every test in this folder needs a candidate, a profile and a ranking context, and constructing
/// them inline three dozen times would bury the one thing each test is actually about.
/// </para>
/// </summary>
internal static class RecommendationTestData
{
    public static readonly DateTimeOffset Now = new(2026, 8, 4, 12, 0, 0, TimeSpan.Zero);

    public static RecommendationCandidate Candidate(
        RecommendationTargetType targetType = RecommendationTargetType.Guide,
        string title = "A guide",
        string? countryCode = "MU",
        string? cityName = null,
        string? neighborhoodName = null,
        string? categoryKey = "immigration",
        bool isVerified = false,
        decimal popularity = 0.5m,
        decimal quality = 0.5m,
        long engagement = 100,
        UserId? owner = null,
        DateTimeOffset? publishedAt = null,
        DateTimeOffset? expiresAt = null,
        CandidateVisibility visibility = CandidateVisibility.Members,
        int minimumTrustLevel = 0,
        IEnumerable<(InterestKind Kind, string Key)>? attributes = null,
        Guid? targetId = null,
        bool isFeatured = false,
        decimal promotionWeight = 0m,
        DateTimeOffset? promotedUntil = null)
    {
        var candidate = RecommendationCandidate.Project(
            RecommendationCandidateId.New(),
            TargetReference.Create(targetType, targetId ?? Guid.NewGuid()),
            "Test",
            title,
            Now);

        candidate.Describe(title, categoryKey: categoryKey);
        candidate.Place(countryCode, cityName: cityName, neighborhoodName: neighborhoodName);
        candidate.Attribute(owner, isVerified, isFeatured);
        candidate.Rate(popularity, quality, engagement);

        if (promotionWeight > 0m)
        {
            candidate.Promote(promotionWeight, promotedUntil);
        }
        candidate.Restrict(visibility, minimumTrustLevel);
        // The update stamp follows the publication unless a test says otherwise. Pinning it
        // independently would make every recency assertion measure the update rather than the
        // publication — which is correct behaviour in the strategy and a trap in a fixture.
        var published = publishedAt ?? Now.AddDays(-7);

        candidate.Schedule(published, published, expiresAt);

        if (attributes is not null)
        {
            candidate.SetAttributes(attributes.Select(attribute =>
                CandidateAttribute.Create(Guid.NewGuid(), attribute.Kind, attribute.Key)));
        }

        return candidate;
    }

    public static PersonalizationProfile Profile(
        UserId? userId = null,
        string? countryCode = "MU",
        string? cityName = null,
        string? neighborhoodName = null,
        bool isVerified = false,
        int trustLevel = 0,
        bool onboardingComplete = true,
        IEnumerable<(InterestKind Kind, string Key, decimal Affinity)>? interests = null,
        int signalCount = 100)
    {
        var profile = PersonalizationProfile.Open(
            PersonalizationProfileId.New(), userId ?? UserId.New(), Now);

        if (countryCode is not null || cityName is not null || neighborhoodName is not null)
        {
            profile.SetLocation(
                countryCode, cityName, neighborhoodName, Now, (_, _) => Guid.NewGuid());
        }

        profile.SetTrust(trustLevel, isVerified);
        profile.SetOnboardingComplete(onboardingComplete);

        foreach (var (kind, key, affinity) in interests ?? [])
        {
            // The affinity moves by a learning rate per observation, so reaching a target value
            // means observing repeatedly. Twenty is enough to get within a rounding of any value.
            for (var i = 0; i < 20; i++)
            {
                profile.ObserveInterest(kind, key, affinity, Now, Guid.NewGuid());
            }
        }

        profile.RecordSignalsFolded(signalCount, Now, Now);

        return profile;
    }

    public static PersonalizationContext Context(
        PersonalizationProfile? profile = null,
        PersonalizationPreference? preference = null,
        RecommendationSurface surface = RecommendationSurface.HomeFeed,
        RecommendationTargetType? targetType = null,
        bool? isColdStart = null,
        AccessTier tier = AccessTier.Limited)
    {
        var resolved = profile ?? Profile();

        return new PersonalizationContext
        {
            UserId = resolved.UserId,
            Profile = resolved,
            Preference = preference,
            Surface = surface,
            TargetType = targetType,
            AsOf = Now,
            IsColdStart = isColdStart ?? resolved.IsColdStart,
            AccessTier = tier
        };
    }

    public static RankingContext RankingContext(
        IReadOnlyList<RecommendationCandidate> candidates,
        RankingStrategy configuration,
        PersonalizationContext? personalization = null,
        IReadOnlySet<string>? engaged = null) =>
        new()
        {
            Personalization = personalization ?? Context(),
            Candidates = candidates,
            Configuration = configuration,
            EngagedTargetKeys = engaged ?? new HashSet<string>(StringComparer.Ordinal)
        };

    public static RankingStrategy Strategy(
        string key,
        RankingStrategyKind kind,
        decimal weight = 1m,
        RecommendationSurface surface = RecommendationSurface.HomeFeed,
        IReadOnlyDictionary<string, string>? parameters = null)
    {
        var strategy = RankingStrategy.Configure(
            RankingStrategyId.New(), key, kind, surface, "Test strategy", weight, Now);

        foreach (var (name, value) in parameters ?? new Dictionary<string, string>())
        {
            strategy.SetParameter(name, value, Guid.NewGuid());
        }

        return strategy;
    }

    public static PersonalizationPreference Preference(
        UserId? userId = null,
        bool personalizationEnabled = true,
        bool shareSignals = true)
    {
        var preference = PersonalizationPreference.Open(
            PersonalizationPreferenceId.New(), userId ?? UserId.New(), Now);

        preference.SetPersonalizationEnabled(personalizationEnabled);
        preference.SetShareActivitySignals(shareSignals);

        return preference;
    }
}
