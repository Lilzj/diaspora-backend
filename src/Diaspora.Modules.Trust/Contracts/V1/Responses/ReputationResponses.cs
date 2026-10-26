namespace Diaspora.Modules.Trust.Contracts.V1.Responses;

/// <summary>
/// A member's full reputation. Matches the frontend's <c>UserReputation</c> field-for-field.
/// <para>
/// Thirty fields is a lot for one DTO, and it is right here: the reputation screen renders
/// each of them as its own line, and the alternative — the client computing the detail
/// counters from an event feed — is how server and client end up showing different numbers
/// for the same member.
/// </para>
/// </summary>
public sealed record ReputationProfileResponse
{
    public required Guid UserId { get; init; }

    // Three dimensions
    public required int ContributionScore { get; init; }
    public required int VerificationScore { get; init; }
    public required int TrustScore { get; init; }
    public required int CombinedScore { get; init; }

    public required string Level { get; init; }

    /// <summary>Null at the top of the ladder.</summary>
    public NextLevelResponse? NextLevel { get; init; }

    // Contribution detail
    public required int GuidesPublished { get; init; }
    public required int GuidesApproved { get; init; }
    public required int UpdatesPublished { get; init; }
    public required int UpdatesVerified { get; init; }
    public required int CommentsWritten { get; init; }
    public required int GuidesUpdated { get; init; }

    // Verification detail
    public required bool PhoneVerified { get; init; }
    public required bool EmailVerified { get; init; }
    public required bool GovernmentIdVerified { get; init; }
    public required bool ProfilePhotoAdded { get; init; }
    public required bool ProfileCompleted { get; init; }
    public required bool ProviderVerified { get; init; }
    public required int CountryVerificationsCount { get; init; }

    // Trust detail
    public required int HelpfulVotesReceived { get; init; }
    public required int CommunityConfirmationsReceived { get; init; }
    public required int ViolationCount { get; init; }
    public required int ReportsResolved { get; init; }
    public required int NoViolationStreakDays { get; init; }

    public required IReadOnlyList<BadgeResponse> Badges { get; init; }

    public required LevelBenefitsResponse LevelBenefits { get; init; }

    public required DateTimeOffset LastCalculatedAt { get; init; }

    public DateTimeOffset? LevelChangedAt { get; init; }

    /// <summary>
    /// What each dimension's score is made of: contribution, verification and trust, always all
    /// three and in that order.
    /// <para>
    /// The counters above say how often something happened; they never said what it was worth, and
    /// the screen multiplied them by figures of its own that the server does not pay. These rows are
    /// totalled from the scoring history itself, so they are the points actually recorded.
    /// </para>
    /// </summary>
    public required IReadOnlyList<ReputationDimensionBreakdownResponse> Breakdown { get; init; }
}

/// <summary>
/// One dimension's score and the rows that make it up.
/// <para>
/// <b><see cref="Score"/> = Σ <see cref="Lines"/>.Points + <see cref="Adjustment"/>, always.</b>
/// The adjustment is non-zero where the score was held at zero — a penalty larger than the score
/// records its full amount and moves the score by less — or, on history scored before scoring
/// was concurrency-checked, where two awards raced and one was lost from the score. Sent rather
/// than hidden, so the rows always add up and the reason they otherwise would not is visible.
/// </para>
/// </summary>
public sealed record ReputationDimensionBreakdownResponse
{
    /// <summary>Lowercase: contribution, verification, trust.</summary>
    public required string Dimension { get; init; }

    public required int Score { get; init; }

    /// <summary>One row per action, largest award first, so penalties come last.</summary>
    public required IReadOnlyList<ReputationBreakdownLineResponse> Lines { get; init; }

    /// <summary>
    /// The score less the sum of the rows. Positive when a penalty went below zero; zero almost
    /// everywhere else.
    /// </summary>
    public required int Adjustment { get; init; }
}

/// <summary>Everything one action has paid on one dimension.</summary>
public sealed record ReputationBreakdownLineResponse
{
    /// <summary>The scoring rule's action — "GuidePublished", "ViolationRecorded".</summary>
    public required string ActionType { get; init; }

    /// <summary>What to call the row — "Guides published", "Policy violations".</summary>
    public required string Label { get; init; }

    /// <summary>How many times it was recorded.</summary>
    public required int Count { get; init; }

    /// <summary>Signed: the total those records carried. Penalties are negative.</summary>
    public required int Points { get; init; }
}

/// <summary>Matches the frontend's <c>UserReputation.nextLevel</c>.</summary>
public sealed record NextLevelResponse
{
    public required string Name { get; init; }

    public required int MinScore { get; init; }

    public required int PointsNeeded { get; init; }
}

/// <summary>Matches the frontend's <c>LevelBenefits</c>.</summary>
public sealed record LevelBenefitsResponse
{
    public required bool CanPublishImmediately { get; init; }
    public required bool CanModerate { get; init; }
    public required bool IsFeaturedEligible { get; init; }
    public required bool IsRewardsEligible { get; init; }
    public required bool HasBetaAccess { get; init; }
    public required bool HasExclusiveBadge { get; init; }
    public required decimal SearchBoostFactor { get; init; }
}

/// <summary>
/// One thing the platform pays reputation for, as <c>GET /trust/reputation/rules</c> lists it.
/// <para>
/// The reputation screen's "How to earn" was a sentence typed into the app: three of its four
/// figures were wrong, and one described a payment that does not exist. It is built from these
/// now, so the copy changes when the rules do.
/// </para>
/// </summary>
public sealed record ReputationRuleResponse
{
    public required string ActionType { get; init; }

    /// <summary>"Guides published", "Policy violations" — the same names the breakdown rows use.</summary>
    public required string Label { get; init; }

    /// <summary>Lowercase: contribution, verification, trust.</summary>
    public required string Dimension { get; init; }

    /// <summary>Signed. Penalties are negative.</summary>
    public required int Points { get; init; }

    /// <summary>How many times it can ever count. Null means uncapped.</summary>
    public int? MaxOccurrences { get; init; }

    /// <summary>The minimum gap between two awards, in hours. Null means none.</summary>
    public int? CooldownHours { get; init; }

    /// <summary>Whether it also counts toward the member's standing in that country.</summary>
    public required bool IsCountryScoped { get; init; }

    /// <summary>
    /// The reward points it also pays (D-2). Null pays none: only confirmed outcomes pay points.
    /// Additive.
    /// </summary>
    public int? CreditPoints { get; init; }
}

/// <summary>
/// One scoring event. Matches the frontend's <c>ReputationEvent</c>, which is declared but
/// never fetched — the history route is what makes it useful.
/// </summary>
public sealed record ReputationEventResponse
{
    public required Guid EventId { get; init; }

    public required Guid UserId { get; init; }

    public required string EventType { get; init; }

    /// <summary>Lowercase: contribution, verification, trust.</summary>
    public required string Dimension { get; init; }

    /// <summary>Signed. Violations are negative.</summary>
    public required int Points { get; init; }

    public string? SourceType { get; init; }

    public string? SourceId { get; init; }

    public string? CountryCode { get; init; }

    public string? Description { get; init; }

    /// <summary>The version of the rules that priced it. Null before the rules were versioned.</summary>
    public int? RuleSetVersion { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
