using Diaspora.Modules.Recommendations.Contracts.V1.Responses;
using Diaspora.Modules.Recommendations.Domain.Analytics;
using Diaspora.Modules.Recommendations.Domain.Candidates;
using Diaspora.Modules.Recommendations.Domain.Policies;
using Diaspora.Modules.Recommendations.Domain.Preferences;
using Diaspora.Modules.Recommendations.Domain.Profiles;
using Diaspora.Modules.Recommendations.Domain.Strategies;
using Diaspora.Modules.Recommendations.Domain.ValueObjects;
using Diaspora.Modules.Recommendations.Extensibility.Engine;
using Diaspora.Modules.Recommendations.Extensibility.Feed;
using Diaspora.Modules.Recommendations.Extensibility.Search;

namespace Diaspora.Modules.Recommendations.Application.Mapping;

/// <summary>
/// The single translation from this module's domain to its wire contract.
/// <para>
/// <b>Static and stateless, which the Release-configuration analyzers require and which is also
/// correct.</b> A mapper with a dependency would be a mapper that could query, and a mapper that
/// queries is how an N+1 gets into a page of forty cards without anybody noticing.
/// </para>
/// <para>
/// <b>One class rather than one per aggregate, because the whole value is that the shapes cannot
/// drift.</b> Two mappers producing a <c>RecommendationResponse</c> would eventually produce two
/// slightly different ones, and the client would meet whichever the endpoint it called happened to
/// use.
/// </para>
/// </summary>
public static class RecommendationMapper
{
    public static RecommendationResponse ToResponse(ScoredRecommendation scored, int position)
    {
        ArgumentNullException.ThrowIfNull(scored);

        var candidate = scored.Candidate;

        return new RecommendationResponse
        {
            TargetType = candidate.Target.TargetType.ToString(),
            TargetId = candidate.Target.TargetId,
            Title = candidate.Title,
            Summary = candidate.Summary,
            Slug = candidate.Slug,
            MediaReference = candidate.MediaReference,
            CategoryKey = candidate.CategoryKey,
            Location = ToResponse(candidate),
            IsVerified = candidate.IsVerified,
            IsFeatured = candidate.IsFeatured,
            PublishedAt = candidate.PublishedAt,
            Position = position,
            Score = scored.Score,
            Strategy = scored.PrimaryStrategyKey,
            Reason = ToResponse(scored.Explanation),
            Contributions = scored.Contributions.Select(ToResponse).ToList()
        };
    }

    public static IReadOnlyList<RecommendationResponse> ToResponses(
        IReadOnlyList<ScoredRecommendation> scored) =>
        scored.Select((item, index) => ToResponse(item, index)).ToList();

    public static RecommendationLocationResponse ToResponse(RecommendationCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return new RecommendationLocationResponse
        {
            CountryCode = candidate.CountryCode,
            RegionName = candidate.RegionName,
            CityName = candidate.CityName,
            NeighborhoodName = candidate.NeighborhoodName
        };
    }

    public static RecommendationReasonResponse ToResponse(RecommendationExplanation explanation)
    {
        ArgumentNullException.ThrowIfNull(explanation);

        return new RecommendationReasonResponse
        {
            Code = explanation.ReasonCode.ToString(),
            ResourceKey = explanation.ResourceKey,
            Text = explanation.FallbackText,
            Confidence = explanation.Confidence.ToString(),
            SignalKinds = explanation.ParseSignalKinds().Select(kind => kind.ToString()).ToList()
        };
    }

    public static StrategyContributionResponse ToResponse(StrategyContribution contribution)
    {
        ArgumentNullException.ThrowIfNull(contribution);

        return new StrategyContributionResponse
        {
            StrategyKey = contribution.StrategyKey,
            Kind = contribution.Kind.ToString(),
            RawScore = contribution.RawScore,
            Weight = contribution.Weight,
            WeightedScore = contribution.WeightedScore
        };
    }

    public static RecommendationPageResponse ToResponse(
        RecommendationResult result, Domain.Enums.RecommendationSurface surface, DateTimeOffset generatedAt)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new RecommendationPageResponse
        {
            Items = ToResponses(result.Items),
            SetId = result.SetId?.Value,
            Surface = surface.ToString(),
            IsColdStart = result.IsColdStart,
            IsCached = result.WasCached,
            CandidateCount = result.CandidateCount,
            ExcludedCount = result.ExcludedCount,
            Strategies = result.StrategyKeys,
            GeneratedAt = generatedAt
        };
    }

    public static FeedSectionResponse ToResponse(ComposedFeedSection section)
    {
        ArgumentNullException.ThrowIfNull(section);

        return new FeedSectionResponse
        {
            Key = section.Definition.Key,
            Title = section.Definition.Title,
            TitleResourceKey = section.Definition.TitleResourceKey,
            TargetType = section.Definition.TargetType?.ToString(),
            Items = ToResponses(section.Items),
            SeeAllRoute = section.Definition.SeeAllRoute,
            IsColdStart = section.IsColdStart
        };
    }

    public static PersonalizationProfileResponse ToResponse(PersonalizationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return new PersonalizationProfileResponse
        {
            Completeness = profile.Completeness,
            IsColdStart = profile.IsColdStart,
            SignalCount = profile.SignalCount,
            PreferredLanguage = profile.PreferredLanguage,
            PrimaryCountryCode = profile.PrimaryCountryCode,
            PrimaryCityName = profile.PrimaryCityName,
            PrimaryNeighborhoodName = profile.PrimaryNeighborhoodName,
            IsVerified = profile.IsVerified,
            HasCompletedOnboarding = profile.HasCompletedOnboarding,

            // Strongest first, and capped: a member with two hundred interests does not want to read
            // two hundred rows, and the tail is noise by construction.
            Interests = profile.TopInterests(50).Select(ToResponse).ToList(),
            Locations = profile.LocationAffinities
                .OrderByDescending(affinity => affinity.IsPrimary)
                .ThenByDescending(affinity => affinity.Affinity)
                .Select(ToResponse)
                .ToList(),
            Activity = profile.ActivitySummaries
                .OrderByDescending(summary => summary.TotalCount)
                .Select(ToResponse)
                .ToList(),
            LastSignalAt = profile.LastSignalAt,
            LastBuiltAt = profile.LastBuiltAt,
            LastRebuiltAt = profile.LastRebuiltAt
        };
    }

    public static ProfileInterestResponse ToResponse(ProfileInterest interest)
    {
        ArgumentNullException.ThrowIfNull(interest);

        return new ProfileInterestResponse
        {
            Kind = interest.Kind.ToString(),
            Key = interest.Key,
            Affinity = interest.Affinity,
            ObservationCount = interest.ObservationCount,
            IsExplicit = interest.IsExplicit,
            LastObservedAt = interest.LastObservedAt
        };
    }

    public static ProfileLocationAffinityResponse ToResponse(ProfileLocationAffinity affinity)
    {
        ArgumentNullException.ThrowIfNull(affinity);

        return new ProfileLocationAffinityResponse
        {
            Scope = affinity.Scope.ToString(),
            Key = affinity.Key,
            DisplayName = affinity.DisplayName,
            Affinity = affinity.Affinity,
            IsPrimary = affinity.IsPrimary
        };
    }

    public static ProfileActivityResponse ToResponse(ProfileActivitySummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new ProfileActivityResponse
        {
            TargetType = summary.TargetType.ToString(),
            ViewCount = summary.ViewCount,
            SaveCount = summary.SaveCount,
            ApplyCount = summary.ApplyCount,
            TransactCount = summary.TransactCount,
            DismissCount = summary.DismissCount,
            LastActivityAt = summary.LastActivityAt
        };
    }

    /// <summary>
    /// The caller's preferences, or the defaults when they have stated nothing.
    /// <para>
    /// Null is mapped rather than refused so the settings screen renders without a special case —
    /// see <see cref="PersonalizationPreferencesResponse"/>.
    /// </para>
    /// </summary>
    public static PersonalizationPreferencesResponse ToResponse(PersonalizationPreference? preference) =>
        preference is null
            ? new PersonalizationPreferencesResponse
            {
                IsPersonalizationEnabled = true,
                ShareActivitySignals = true,
                Categories = [],
                BlockedTargets = [],
                Surfaces = [],
                FavoriteLocations = []
            }
            : new PersonalizationPreferencesResponse
            {
                IsPersonalizationEnabled = preference.IsPersonalizationEnabled,
                ShareActivitySignals = preference.ShareActivitySignals,
                PreferredLanguage = preference.PreferredLanguage,
                PreferredCountryCode = preference.PreferredCountryCode,
                Categories = preference.Categories.Select(ToResponse).ToList(),
                BlockedTargets = preference.BlockedTargets.Select(ToResponse).ToList(),
                Surfaces = preference.Surfaces.Select(ToResponse).ToList(),
                FavoriteLocations = preference.FavoriteLocations.Select(ToResponse).ToList()
            };

    public static CategoryPreferenceResponse ToResponse(CategoryPreference preference)
    {
        ArgumentNullException.ThrowIfNull(preference);

        return new CategoryPreferenceResponse
        {
            CategoryKey = preference.CategoryKey,
            TargetType = preference.TargetType?.ToString(),
            IsMuted = preference.IsMuted
        };
    }

    public static BlockedTargetResponse ToResponse(BlockedRecommendationTarget block)
    {
        ArgumentNullException.ThrowIfNull(block);

        return new BlockedTargetResponse
        {
            TargetType = block.TargetType.ToString(),
            TargetId = block.TargetId,
            BlockedAt = block.BlockedAt
        };
    }

    public static SurfacePreferenceResponse ToResponse(SurfacePreference preference)
    {
        ArgumentNullException.ThrowIfNull(preference);

        return new SurfacePreferenceResponse
        {
            Surface = preference.Surface.ToString(),
            IsEnabled = preference.IsEnabled
        };
    }

    public static FavoriteLocationResponse ToResponse(FavoriteLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);

        return new FavoriteLocationResponse
        {
            Scope = location.Scope.ToString(),
            Key = location.Key,
            DisplayName = location.DisplayName
        };
    }

    public static RankingStrategyResponse ToResponse(RankingStrategy strategy, bool isImplemented)
    {
        ArgumentNullException.ThrowIfNull(strategy);

        return new RankingStrategyResponse
        {
            StrategyId = strategy.Id.Value,
            Key = strategy.Key,
            Kind = strategy.Kind.ToString(),
            Surface = strategy.Surface.ToString(),
            TargetType = strategy.TargetType?.ToString(),
            Description = strategy.Description,
            Weight = strategy.Weight,
            Order = strategy.Order,
            IsEnabled = strategy.IsEnabled,
            RunsOnColdStart = strategy.RunsOnColdStart,
            MinimumScore = strategy.MinimumScore,
            Parameters = strategy.Parameters.ToDictionary(
                parameter => parameter.Key, parameter => parameter.Value, StringComparer.Ordinal),
            IsImplemented = isImplemented
        };
    }

    public static RecommendationPolicyResponse ToResponse(
        RecommendationPolicy policy, bool isImplemented)
    {
        ArgumentNullException.ThrowIfNull(policy);

        return new RecommendationPolicyResponse
        {
            PolicyId = policy.Id.Value,
            Key = policy.Key,
            Kind = policy.Kind.ToString(),
            Effect = policy.Effect.ToString(),
            Description = policy.Description,
            IsEnabled = policy.IsEnabled,
            IsSystem = policy.IsSystem,
            Order = policy.Order,
            DemotionFactor = policy.DemotionFactor,
            Surfaces = Split(policy.Surfaces),
            TargetTypes = Split(policy.TargetTypes),
            Parameters = policy.Parameters.ToDictionary(
                parameter => parameter.Key, parameter => parameter.Value, StringComparer.Ordinal),
            IsImplemented = isImplemented
        };
    }

    public static RecommendationAnalyticsResponse ToResponse(RecommendationAnalytics analytics)
    {
        ArgumentNullException.ThrowIfNull(analytics);

        return new RecommendationAnalyticsResponse
        {
            Day = analytics.Day,
            Surface = analytics.Surface.ToString(),
            GeneratedSetCount = analytics.GeneratedSetCount,
            GeneratedItemCount = analytics.GeneratedItemCount,
            ViewCount = analytics.ViewCount,
            ClickCount = analytics.ClickCount,
            AcceptCount = analytics.AcceptCount,
            DismissCount = analytics.DismissCount,
            BookmarkCount = analytics.BookmarkCount,
            ColdStartSetCount = analytics.ColdStartSetCount,
            PolicyExclusionCount = analytics.PolicyExclusionCount,
            AcceptanceRate = analytics.AcceptanceRate,
            ClickThroughRate = analytics.ClickThroughRate,
            DismissalRate = analytics.DismissalRate,
            DiversityScore = analytics.DiversityScore,
            ColdStartSuccessRate = analytics.ColdStartSuccessRate,
            AverageProfileCompleteness = analytics.AverageProfileCompleteness,
            AverageGenerationMilliseconds = analytics.AverageGenerationMilliseconds,
            MaxGenerationMilliseconds = analytics.GenerationMillisecondsMax,
            AverageSignalFreshnessHours = analytics.AverageSignalFreshnessHours,
            Breakdowns = analytics.Breakdowns.Select(ToResponse).ToList()
        };
    }

    public static AnalyticsBreakdownResponse ToResponse(AnalyticsBreakdown breakdown)
    {
        ArgumentNullException.ThrowIfNull(breakdown);

        return new AnalyticsBreakdownResponse
        {
            Dimension = breakdown.Dimension,
            Key = breakdown.Key,
            GeneratedCount = breakdown.GeneratedCount,
            ViewCount = breakdown.ViewCount,
            ClickCount = breakdown.ClickCount,
            AcceptCount = breakdown.AcceptCount,
            DismissCount = breakdown.DismissCount,
            AcceptanceRate = breakdown.AcceptanceRate,
            ClickThroughRate = breakdown.ClickThroughRate,
            DismissalRate = breakdown.DismissalRate
        };
    }

    public static SearchBoostResponse ToResponse(SearchBoostSignal signal)
    {
        ArgumentNullException.ThrowIfNull(signal);

        return new SearchBoostResponse
        {
            Field = signal.Field,
            Value = signal.Value,
            Boost = signal.Boost,
            ReasonCode = signal.ReasonCode.ToString()
        };
    }

    /// <summary>
    /// Parses a comma-separated scope list.
    /// <para>
    /// Empty means "every", which is why an empty string maps to an empty list rather than to a list
    /// containing one empty entry — the difference decides whether a policy applies everywhere or
    /// nowhere.
    /// </para>
    /// </summary>
    private static string[] Split(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
