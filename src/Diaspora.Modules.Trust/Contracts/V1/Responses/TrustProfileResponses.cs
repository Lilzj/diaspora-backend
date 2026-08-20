namespace Diaspora.Modules.Trust.Contracts.V1.Responses;

/// <summary>
/// Everything the Trust &amp; Safety Center needs, in one call.
/// <para>
/// The frontend's <c>GET /trust/profile</c> mock returns only
/// <c>{ healthStatus, riskScore, verifications }</c> and the screen derives the rest locally
/// — trust level from the auth store, country standings and the journey from seeded Zustand
/// state, feature gates from a client-side computation. All of it moves here, because a
/// trust centre assembled from four sources on the device is a trust centre that can disagree
/// with the server about whether the member is verified.
/// </para>
/// </summary>
public sealed record TrustProfileResponse
{
    public required Guid UserId { get; init; }

    /// <summary>Untrusted, Basic, Provisional, Verified, Trusted, Steward.</summary>
    public required string TrustLevel { get; init; }

    public required AccountHealthStatusResponse HealthStatus { get; init; }

    /// <summary>0–100. Staff-facing; omitted from the public projection.</summary>
    public int? RiskScore { get; init; }

    /// <summary>Low, Medium, High. Omitted from the public projection.</summary>
    public string? RiskLevel { get; init; }

    /// <summary>
    /// Status per verification type, keyed by type name — the
    /// <c>Record&lt;VerificationType, VerificationStatus&gt;</c> shape the checklist renders
    /// from. Every supported type appears; types never attempted read <c>"Pending"</c>, which
    /// is what the client's seeded store already assumes.
    /// </summary>
    public required IReadOnlyDictionary<string, string> Verifications { get; init; }

    public required IReadOnlyList<BadgeResponse> Badges { get; init; }

    public required IReadOnlyList<CountryStandingResponse> CountryReputations { get; init; }

    /// <summary>The frontend's <c>CountryExperienceInterval[]</c>.</summary>
    public required IReadOnlyList<CountryExperienceResponse> ExperienceTimeline { get; init; }

    public required DateTimeOffset JoinedAt { get; init; }

    public DateTimeOffset? TrustLevelChangedAt { get; init; }
}

/// <summary>
/// Another member's trust standing, as anyone may see it.
/// <para>
/// Deliberately a different type rather than the same one with nulls: risk score, health
/// reasons and violation counts must be structurally absent from a public projection, not
/// merely unset. A field that is sometimes populated eventually leaks.
/// </para>
/// </summary>
public sealed record PublicTrustProfileResponse
{
    public required Guid UserId { get; init; }

    public required string TrustLevel { get; init; }

    /// <summary>Member, VerifiedResident, TrustedContributor, CommunityExpert, CommunityAmbassador.</summary>
    public required string ReputationLevel { get; init; }

    public required IReadOnlyList<BadgeResponse> Badges { get; init; }

    public required IReadOnlyList<CountryStandingResponse> CountryReputations { get; init; }

    public required bool IsVerifiedMember { get; init; }

    public required DateTimeOffset MemberSince { get; init; }
}

/// <summary>Matches the frontend's <c>AccountHealthStatus</c>.</summary>
public sealed record AccountHealthStatusResponse
{
    /// <summary>
    /// Excellent, Good, Restricted, Suspended, UnderReview.
    /// <para>
    /// The last one is <c>'Under Review'</c> with a space on the client. A space cannot
    /// survive enum binding, so the server emits <c>UnderReview</c> and the client adds one
    /// entry to the label map it already uses for display.
    /// </para>
    /// </summary>
    public required string Health { get; init; }

    public required int ViolationsCount { get; init; }

    public required int WarningsCount { get; init; }

    public required int ActiveAppealsCount { get; init; }

    public required int SpamReportsCount { get; init; }

    public required IReadOnlyList<string> Reasons { get; init; }
}

/// <summary>
/// One badge. Matches the frontend's <c>ReputationBadge</c>, plus the fields a revocable
/// award needs.
/// </summary>
public sealed record BadgeResponse
{
    public required string Type { get; init; }

    /// <summary>Display text, already localised and emoji-prefixed by the server.</summary>
    public required string Label { get; init; }

    /// <summary>Year first earned. The client renders "since 2024".</summary>
    public int? Since { get; init; }

    public string? CountryCode { get; init; }

    public required DateTimeOffset AwardedAt { get; init; }
}

/// <summary>Matches the frontend's <c>CountryReputationScore</c>.</summary>
public sealed record CountryStandingResponse
{
    public required string CountryCode { get; init; }

    public required string CountryName { get; init; }

    /// <summary>Emoji flag. Derived from the country code server-side.</summary>
    public required string Flag { get; init; }

    public required int Points { get; init; }

    /// <summary>
    /// The reputation level in this country.
    /// <para>
    /// Emits <c>CommunityAmbassador</c>, not the <c>Ambassador</c> the frontend's
    /// <c>CountryReputationScore.level</c> declares — that union contradicts the same file's
    /// <c>ReputationLevel</c>, and the longer name is the one with thresholds behind it.
    /// </para>
    /// </summary>
    public required string Level { get; init; }

    /// <summary>1–5.</summary>
    public required int Stars { get; init; }

    public required bool IsVerified { get; init; }
}

/// <summary>Matches the frontend's <c>CountryExperienceInterval</c>.</summary>
public sealed record CountryExperienceResponse
{
    public required string CountryCode { get; init; }

    public required string CountryName { get; init; }

    public required string Flag { get; init; }

    public required int StartYear { get; init; }

    /// <summary>Null means "Present".</summary>
    public int? EndYear { get; init; }

    public required bool IsVerified { get; init; }

    public required string Role { get; init; }

    public required int YearsOfResidence { get; init; }
}

/// <summary>One entry on the trust timeline. Matches the frontend's <c>TrustTimelineEvent</c>.</summary>
public sealed record TrustTimelineEventResponse
{
    public required Guid EventId { get; init; }

    public required string Type { get; init; }

    public required string Label { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Emoji, chosen server-side so a new event kind renders without an app release.</summary>
    public required string Icon { get; init; }

    public string? Description { get; init; }

    public string? CountryCode { get; init; }
}
