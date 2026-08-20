namespace Diaspora.Modules.Providers.Contracts.V1.Responses;

/// <summary>
/// The counters an owner's dashboard renders.
/// <para>
/// <see cref="ContactCount"/> is the number that actually matters to an owner, and it is the one
/// the client has no concept of today — it renders <c>viewCount</c>, which measures curiosity
/// rather than business.
/// </para>
/// </summary>
public sealed record ProviderStatisticsResponse
{
    public required long ViewCount { get; init; }

    /// <summary>Calls, messages and directions taps.</summary>
    public required long ContactCount { get; init; }

    /// <summary>Times the provider appeared in a result set. The denominator for a click rate.</summary>
    public required long SearchImpressionCount { get; init; }

    public required long BookmarkCount { get; init; }

    public required long ShareCount { get; init; }

    /// <summary>Projected from Community. The client's <c>followersCount</c>.</summary>
    public required long FollowerCount { get; init; }

    /// <summary>Projected from Community. The client's <c>postsCount</c>.</summary>
    public required long PostCount { get; init; }

    public required int ServiceCount { get; init; }

    /// <summary>The client's <c>membersCount</c>.</summary>
    public required int MemberCount { get; init; }

    public required int ReviewCount { get; init; }

    public required int RecommendationCount { get; init; }

    /// <summary>0–100. Drives the onboarding nudge, and is one input to the default ranking.</summary>
    public required int ProfileCompletionPercent { get; init; }
}
