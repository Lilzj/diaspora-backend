namespace Diaspora.Modules.CommunityProgramme.Contracts.V1.Responses;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// Statistics, comparison, rules, notices, role holders and preferences — 21 Oct 2026.
//
// Shaped to `src/types/community-directory.ts` in the app, field for field: CommunityStatistics,
// CommunityMetric, CommunityComparison, CommunityRule, CommunityNotice, CommunityRoleHolder and
// CommunityPreferences. Where this adds a field the app does not declare yet — the statistics'
// `unavailableMetrics` and `series`, the preferences' `defaultMembershipVisibility` — the addition is
// additive: a client that ignores it reads the same object it always did.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// When a figure was computed and how far to trust it — the app's <c>DataFreshness</c>.
/// </summary>
public sealed record DataFreshnessResponse
{
    public required DateTimeOffset ComputedAt { get; init; }

    /// <summary>Never blank.</summary>
    public required string SourceName { get; init; }

    public string? Methodology { get; init; }

    public IReadOnlyList<string>? Limitations { get; init; }

    public int? SampleSize { get; init; }
}

/// <summary>One figure on a community's profile — the app's <c>CommunityMetric</c>.</summary>
public sealed record CommunityMetricResponse
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    /// <summary>Always <c>source: "backend"</c>. Nothing on this route is estimated.</summary>
    public required SourcedResponse<int> Value { get; init; }

    public string? DisplayValue { get; init; }

    public string? Unit { get; init; }

    /// <summary>Against the previous period of the same length, where that period had any.</summary>
    public SourcedResponse<double>? ChangePercent { get; init; }

    public required string Period { get; init; }

    public required DataFreshnessResponse Freshness { get; init; }

    /// <summary>
    /// Served only to callers with <c>canViewPrivateStatistics</c>. Everybody else never receives
    /// the metric at all — not a null — because a nulled restricted metric still says it exists.
    /// </summary>
    public required bool IsRestricted { get; init; }
}

/// <summary>A figure the platform cannot produce, named so the screen can say so rather than show zero.</summary>
public sealed record UnavailableMetricResponse
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    public required string Reason { get; init; }
}

/// <summary>One bucket of a series.</summary>
public sealed record StatisticsPointResponse
{
    /// <summary>The bucket's start, inclusive. The next point's start is its end.</summary>
    public required DateTimeOffset Start { get; init; }

    public required int Value { get; init; }
}

/// <summary>A metric over the period, bucketed by <c>interval</c>.</summary>
public sealed record StatisticsSeriesResponse
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    /// <summary><c>day</c>, <c>week</c> or <c>month</c>.</summary>
    public required string Interval { get; init; }

    public required IReadOnlyList<StatisticsPointResponse> Points { get; init; }
}

/// <summary>A community's figures for a period — the app's <c>CommunityStatistics</c>.</summary>
public sealed record CommunityStatisticsResponse
{
    public required Guid CommunityId { get; init; }

    /// <summary>7d, 30d, 90d, 12m or all.</summary>
    public required string Period { get; init; }

    /// <summary>The window the figures cover: <c>[from, until)</c>.</summary>
    public required DateTimeOffset From { get; init; }

    public required DateTimeOffset Until { get; init; }

    public required IReadOnlyList<CommunityMetricResponse> Metrics { get; init; }

    /// <summary>Figures the screen may expect and the platform cannot count, with the reason.</summary>
    public required IReadOnlyList<UnavailableMetricResponse> UnavailableMetrics { get; init; }

    public required IReadOnlyList<StatisticsSeriesResponse> Series { get; init; }

    /// <summary>Always <c>backend</c>.</summary>
    public required string Source { get; init; }

    public required DataFreshnessResponse Freshness { get; init; }
}

/// <summary>A community named in a comparison — the app's <c>CommunityRef</c>.</summary>
public sealed record CommunityRefResponse
{
    public required Guid Id { get; init; }

    public required string Slug { get; init; }

    public required string Name { get; init; }

    public required string Type { get; init; }

    public required CommunityOriginResponse Origin { get; init; }

    public required CommunityIdentityResponse Identity { get; init; }
}

/// <summary>One metric across the compared communities, aligned to them by index.</summary>
public sealed record CommunityComparisonRowResponse
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    /// <summary>Null where that community does not report the metric to this caller.</summary>
    public required IReadOnlyList<SourcedResponse<int>?> Values { get; init; }

    public string? Unit { get; init; }
}

/// <summary>Communities side by side — the app's <c>CommunityComparison</c>. No winner, no score.</summary>
public sealed record CommunityComparisonResponse
{
    public required string Period { get; init; }

    public required IReadOnlyList<CommunityRefResponse> Communities { get; init; }

    public required IReadOnlyList<CommunityComparisonRowResponse> Rows { get; init; }

    public required string Source { get; init; }

    public required DataFreshnessResponse Freshness { get; init; }
}

/// <summary>A rule — the app's <c>CommunityRule</c>.</summary>
public sealed record CommunityRuleResponse
{
    public required Guid Id { get; init; }

    public required int Position { get; init; }

    public required string Title { get; init; }

    public required string Body { get; init; }

    public string? Consequence { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }
}

/// <summary>A notice — the app's <c>CommunityNotice</c>.</summary>
public sealed record CommunityNoticeResponse
{
    public required Guid Id { get; init; }

    public required Guid CommunityId { get; init; }

    public required string Title { get; init; }

    public required string Body { get; init; }

    public required DateTimeOffset PublishedAt { get; init; }

    /// <summary>The publisher's highest role when they published. Attribution, not authority.</summary>
    public required string PublishedByRole { get; init; }

    public string? PublishedByName { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public required bool IsPinned { get; init; }

    /// <summary>Whether this caller may revise or remove it. The server checks again on the write.</summary>
    public required bool CanEdit { get; init; }
}

/// <summary>A person holding a role, as the community publishes them — the app's <c>CommunityRoleHolder</c>.</summary>
public sealed record CommunityRoleHolderResponse
{
    public required Guid UserId { get; init; }

    public required string DisplayName { get; init; }

    public string? AvatarUrl { get; init; }

    /// <summary>The highest role they hold, or the one filtered on.</summary>
    public required string Role { get; init; }

    /// <summary>When they joined the community. The platform does not record when a role was given.</summary>
    public DateTimeOffset? Since { get; init; }

    public string? Bio { get; init; }

    public IReadOnlyList<string> Languages { get; init; } = [];

    public string? AreaLabel { get; init; }
}

/// <summary>One notification overlay row — the app's <c>CommunityNotificationPreference</c>.</summary>
public sealed record CommunityNotificationPreferenceResponse
{
    public required string CategoryKey { get; init; }

    public required string Scope { get; init; }

    public IReadOnlyList<Guid> CommunityIds { get; init; } = [];
}

/// <summary>The member's community preferences — the app's <c>CommunityPreferences</c>.</summary>
public sealed record CommunityPreferencesResponse
{
    public required bool CrossCommunityDiscoveryEnabled { get; init; }

    public required bool ShowPrimaryCommunityOnProfile { get; init; }

    public required bool ShowInterestedCommunitiesOnProfile { get; init; }

    public required bool ShowCommunityRolesOnProfile { get; init; }

    public required IReadOnlyList<CommunityNotificationPreferenceResponse> Notifications { get; init; }

    public string? PreferredContentLanguage { get; init; }

    /// <summary>Where a new relationship with a community starts: Private unless the member chose.</summary>
    public required string DefaultMembershipVisibility { get; init; }

    /// <summary>False until the member first saves: these are the defaults, not a stored choice.</summary>
    public required bool IsSaved { get; init; }
}
