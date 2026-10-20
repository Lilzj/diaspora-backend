namespace Diaspora.Contracts.Trust.V1.Reputation;

/// <summary>
/// Read access to a member's reputation and badges, for modules that compose after Trust.
/// <para>
/// <b>Added 21 Oct 2026 for the creator reputation read</b>, which replaces the app's hand-written
/// badge shelf. Trust owns the reputation ledger and the badge awards and implements this; it lives
/// in <c>Diaspora.Contracts</c> so the Creator module reads Trust's figures without referencing
/// Trust's assembly or tables.
/// </para>
/// <para>
/// <b>Read only, and deliberately so.</b> The ledger has two writers already
/// (<c>ScoreAsync</c> and <c>ApplyAwardAsync</c>), and a third door would be how a duplicate award
/// gets paid. Nothing here can move a score.
/// </para>
/// </summary>
public interface IMemberReputationReader
{
    /// <summary>
    /// The member's reputation figures and active badges.
    /// <para>
    /// A member nothing has ever been scored for is a Member on zero — Trust's own answer on
    /// <c>GET /trust/reputation/me</c> — with <see cref="MemberReputationFigures.HasRecord"/> false,
    /// so a caller never has to restate Trust's thresholds to describe them.
    /// </para>
    /// </summary>
    Task<MemberReputationSnapshot> GetAsync(
        Guid memberUserId, CancellationToken cancellationToken = default);
}

/// <summary>What Trust knows about one member's standing.</summary>
public sealed record MemberReputationSnapshot
{
    public required Guid UserId { get; init; }

    public required MemberReputationFigures Reputation { get; init; }

    /// <summary>Active badge awards, oldest first. Revoked awards are absent.</summary>
    public required IReadOnlyList<MemberBadgeAward> Badges { get; init; }
}

/// <summary>The ledger's running totals, as Trust holds them.</summary>
public sealed record MemberReputationFigures
{
    /// <summary>False when nothing has ever been scored and these are a fresh record's zeros.</summary>
    public required bool HasRecord { get; init; }

    public required int ContributionScore { get; init; }

    public required int VerificationScore { get; init; }

    /// <summary>Includes moderation outcomes. A caller decides who may see it; Trust does not.</summary>
    public required int TrustScore { get; init; }

    public required int CombinedScore { get; init; }

    /// <summary>Member, VerifiedResident, TrustedContributor, CommunityExpert, CommunityAmbassador.</summary>
    public required string Level { get; init; }

    /// <summary>The next rung, or null at the top of the ladder.</summary>
    public string? NextLevel { get; init; }

    public int? NextLevelMinimumScore { get; init; }

    public int? PointsToNextLevel { get; init; }

    /// <summary>Guide verifications the ledger has scored.</summary>
    public required int GuidesApproved { get; init; }

    public required int CommunityConfirmationsReceived { get; init; }

    public required DateTimeOffset LastCalculatedAt { get; init; }
}

/// <summary>One active badge award.</summary>
public sealed record MemberBadgeAward
{
    /// <summary>Trust's badge type, e.g. <c>PhoneVerified</c>.</summary>
    public required string Type { get; init; }

    /// <summary>Trust's display text, already emoji-prefixed.</summary>
    public required string Label { get; init; }

    public string? CountryCode { get; init; }

    public required DateTimeOffset AwardedAt { get; init; }
}
