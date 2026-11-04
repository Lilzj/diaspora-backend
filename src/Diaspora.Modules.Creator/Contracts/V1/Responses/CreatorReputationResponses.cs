namespace Diaspora.Modules.Creator.Contracts.V1.Responses;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// A creator's reputation — the app's `CreatorReputation` (src/types/creator-economy.ts), plus the
// two lists that make an absence legible: `signals` (the counts behind the figures) and
// `unavailable` (what the platform cannot compute, with why).
//
// Every figure here is read, never estimated: the score, level and badges are Trust's ledger and
// awards, the badges with progress are this module's milestone catalogue, followers are Community's
// follow graph, guide counts and views are Guides'. Nothing here is derived from anything else.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>GET /creators/me/reputation and GET /creators/{creatorId}/reputation.</summary>
public sealed record CreatorReputationResponse
{
    public required Guid CreatorId { get; init; }

    public required Guid UserId { get; init; }

    /// <summary>
    /// True on the creator's own read. A public read withholds the combined score and the trust
    /// dimension, which carries moderation outcomes; <see cref="Unavailable"/> says so.
    /// </summary>
    public required bool IsSelf { get; init; }

    /// <summary>Trust's combined score. Null on a public read.</summary>
    public int? Score { get; init; }

    /// <summary>Trust's reputation level: Member … CommunityAmbassador.</summary>
    public required string Tier { get; init; }

    public required string TierLabel { get; init; }

    /// <summary>How the score is made up. Uncapped, so <c>maxScore</c> is always null.</summary>
    public required IReadOnlyList<CreatorReputationDimensionResponse> Dimensions { get; init; }

    public required IReadOnlyList<CreatorReputationBadgeResponse> Badges { get; init; }

    /// <summary>Null at the top of the ladder, and on a public read.</summary>
    public int? PointsToNextTier { get; init; }

    public string? NextTier { get; init; }

    public string? NextTierLabel { get; init; }

    /// <summary>The counts behind the figures, each from the module that owns it.</summary>
    public required IReadOnlyList<CreatorReputationSignalResponse> Signals { get; init; }

    /// <summary>What this read cannot answer, and why. Never filled with an estimate.</summary>
    public required IReadOnlyList<CreatorReputationUnavailableResponse> Unavailable { get; init; }

    /// <summary>When Trust last scored the member, or the creator record's own time if never.</summary>
    public required DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>One part of the score.</summary>
public sealed record CreatorReputationDimensionResponse
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    public required int Score { get; init; }

    /// <summary>Always null: Trust does not cap a dimension.</summary>
    public int? MaxScore { get; init; }

    public string? Description { get; init; }
}

/// <summary>
/// A badge — the app's <c>CreatorBadge</c>.
/// <para>
/// Two sources, named in <see cref="Source"/>: <c>trust</c> (an active award, earned; on the
/// creator's own read also each badge still to earn, with how to earn it as its
/// <see cref="Description"/>) and <c>milestone</c> (a creator milestone whose reward is a badge;
/// earned once claimed, otherwise served with its progress on the creator's own read).
/// </para>
/// </summary>
public sealed record CreatorReputationBadgeResponse
{
    public required string BadgeId { get; init; }

    public required string Key { get; init; }

    public required string Label { get; init; }

    public string? Description { get; init; }

    public string? Icon { get; init; }

    /// <summary>Always null: neither source grades its badges.</summary>
    public string? Tier { get; init; }

    public required bool IsEarned { get; init; }

    public DateTimeOffset? EarnedAt { get; init; }

    public CreatorProgressResponse? Progress { get; init; }

    /// <summary><c>trust</c> or <c>milestone</c>.</summary>
    public required string Source { get; init; }

    /// <summary>For a country-scoped Trust badge.</summary>
    public string? CountryCode { get; init; }
}

/// <summary>A count, and the module whose record it is.</summary>
public sealed record CreatorReputationSignalResponse
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    public required long Value { get; init; }

    /// <summary><c>trust</c>, <c>community</c>, <c>guides</c> or <c>creator</c>.</summary>
    public required string Source { get; init; }

    public string? Description { get; init; }
}

/// <summary>Something this read does not serve, and the reason.</summary>
public sealed record CreatorReputationUnavailableResponse
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    public required string Reason { get; init; }
}
