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

    public required DateTimeOffset CreatedAt { get; init; }
}
