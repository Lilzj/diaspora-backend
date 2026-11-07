using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Trust.Domain.Events;
using Diaspora.SharedKernel;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Trust.Domain;

/// <summary>
/// What the platform currently believes about one member's trustworthiness.
/// <para>
/// One per member, created on first contact with the module. It aggregates the standing that
/// individual verifications, appeals and violations produce, and it is the thing every other
/// module ends up asking about through <c>ITrustService</c> — so it is deliberately cheap to
/// read and never needs the verification history loaded to answer "may they publish?".
/// </para>
/// <para>
/// The invariant worth naming: <see cref="Tier"/> is never set directly from outside. It is
/// re-derived from the grants and violations the profile holds, which is what stops a
/// handler somewhere quietly promoting a member without evidence for it.
/// </para>
/// </summary>
public sealed class TrustProfile : AggregateRoot<TrustProfileId>, IAuditableEntity
{
    public const int MaxReasonLength = 500;

    private readonly List<BadgeAward> _badges = [];
    private readonly List<CountryStanding> _countries = [];
    private readonly HashSet<VerificationType> _grants = [];

    public UserId UserId { get; private set; } = default!;

    public TrustTier Tier { get; private set; }

    public AccountHealth Health { get; private set; }

    /// <summary>Confirmed policy violations. Drives health and gates publishing.</summary>
    public int ViolationCount { get; private set; }

    /// <summary>Formal warnings short of a violation.</summary>
    public int WarningCount { get; private set; }

    /// <summary>Appeals currently open. Surfaced so the client can show "under appeal".</summary>
    public int ActiveAppealCount { get; private set; }

    /// <summary>Spam reports upheld against this member.</summary>
    public int SpamReportCount { get; private set; }

    /// <summary>Human-readable reasons behind the current health, for the member to see.</summary>
    public string? HealthReasons { get; private set; }

    /// <summary>Last computed risk. Staff-facing; never in a public projection.</summary>
    public RiskAssessment Risk { get; private set; } = default!;

    /// <summary>When the member's trust journey began.</summary>
    public DateTimeOffset JoinedAt { get; private set; }

    public DateTimeOffset? TierChangedAt { get; private set; }

    public IReadOnlyList<BadgeAward> Badges => _badges.AsReadOnly();

    public IReadOnlyList<CountryStanding> Countries => _countries.AsReadOnly();

    /// <summary>The verification types the member currently holds.</summary>
    public IReadOnlyCollection<VerificationType> Grants => _grants;

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }

    private TrustProfile() { }

    private TrustProfile(TrustProfileId id, UserId userId, DateTimeOffset joinedAt) : base(id)
    {
        UserId = userId;
        Tier = TrustTier.Untrusted;
        Health = AccountHealth.Good;
        JoinedAt = joinedAt;
        Risk = RiskAssessment.Baseline(joinedAt);
    }

    public static TrustProfile Open(UserId userId, DateTimeOffset joinedAt)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return new TrustProfile(TrustProfileId.New(), userId, joinedAt);
    }

    // ── Grants ───────────────────────────────────────────────────────────

    /// <summary>
    /// Records that a verification was granted, awards whatever that implies, and re-derives
    /// the tier. The single entry point from an approved <see cref="VerificationRequest"/>.
    /// </summary>
    /// <param name="arrivedOn">
    /// When the member arrived in <paramref name="countryCode"/>, for a residency grant.
    /// Taken here rather than left to the caller to set on the standing afterwards: the
    /// arrival date is what the residence-duration gate is computed from, and a caller that
    /// forgot to set it would silently produce a verified resident with zero years.
    /// </param>
    public void RecordGrant(
        VerificationType type,
        string? countryCode,
        DateTimeOffset at,
        DateOnly? arrivedOn = null)
    {
        _grants.Add(type);

        switch (type)
        {
            case VerificationType.Phone:
                AwardBadge(BadgeType.PhoneVerified, null, "verification", at);
                break;

            case VerificationType.Email:
                AwardBadge(BadgeType.EmailVerified, null, "verification", at);
                break;

            case VerificationType.Identity:
                AwardBadge(BadgeType.GovernmentIdVerified, null, "verification", at);
                break;

            case VerificationType.Diaspora:
                AwardBadge(BadgeType.VerifiedMember, null, "verification", at);
                break;

            case VerificationType.Country or VerificationType.Residence:
                if (countryCode is not null)
                {
                    StandingIn(countryCode, at).ConfirmResidency(arrivedOn, at);
                    AwardBadge(BadgeType.CountryVerified, countryCode, "verification", at);
                }

                break;

            case VerificationType.Business:
                AwardBadge(BadgeType.VerifiedBusiness, null, "verification", at);
                break;

            case VerificationType.Organization:
                AwardBadge(BadgeType.VerifiedOrganization, null, "verification", at);
                break;

            case VerificationType.Professional or VerificationType.Employment:
                AwardBadge(BadgeType.ProviderVerified, null, "verification", at);
                break;

            case VerificationType.CommunityGuide:
                AwardBadge(BadgeType.CommunityGuide, countryCode, "verification", at);
                break;

            case VerificationType.Contributor:
                AwardBadge(BadgeType.TrustedContributor, countryCode, "verification", at);
                break;
        }

        RederiveTier(at, $"{type} verification granted.");
    }

    /// <summary>Records that a grant was lost — lapsed, revoked, or appealed away.</summary>
    public void RecordGrantLost(VerificationType type, string? countryCode, string reason, DateTimeOffset at)
    {
        _grants.Remove(type);

        if (type is VerificationType.Country or VerificationType.Residence && countryCode is not null)
        {
            StandingIn(countryCode, at).RevokeResidency(at);
            RevokeBadge(BadgeType.CountryVerified, countryCode, reason, at);
        }

        foreach (var badge in BadgesImpliedBy(type))
        {
            RevokeBadge(badge, null, reason, at);
        }

        RederiveTier(at, reason);
    }

    // ── Badges ───────────────────────────────────────────────────────────

    /// <summary>
    /// Awards a badge, or reinstates one previously revoked. Idempotent: awarding a badge the
    /// member already actively holds does nothing and raises no event, so a replayed handler
    /// cannot spam a member with the same congratulation twice.
    /// </summary>
    public BadgeAward AwardBadge(BadgeType badge, string? countryCode, string? awardedBy, DateTimeOffset at)
    {
        var existing = _badges.FirstOrDefault(award => award.Matches(badge, countryCode));

        if (existing is not null)
        {
            if (existing.IsActive)
            {
                return existing;
            }

            existing.Reinstate(at);
            RaiseDomainEvent(new BadgeAwardedDomainEvent(Id, UserId, badge, countryCode, at));
            return existing;
        }

        var award = BadgeAward.Grant(Id, badge, countryCode, awardedBy, at);
        _badges.Add(award);

        RaiseDomainEvent(new BadgeAwardedDomainEvent(Id, UserId, badge, countryCode, at));
        return award;
    }

    /// <summary>Takes a badge back. Silent if the member does not actively hold it.</summary>
    public void RevokeBadge(BadgeType badge, string? countryCode, string reason, DateTimeOffset at)
    {
        var existing = _badges.FirstOrDefault(award => award.Matches(badge, countryCode) && award.IsActive);
        if (existing is null)
        {
            return;
        }

        existing.Revoke(reason, at);
        RaiseDomainEvent(new BadgeRevokedDomainEvent(Id, UserId, badge, countryCode, reason, at));
    }

    public bool HasBadge(BadgeType badge, string? countryCode = null) =>
        _badges.Any(award => award.Matches(badge, countryCode) && award.IsActive);

    // ── Country standing ─────────────────────────────────────────────────

    /// <summary>The member's standing in a country, created on first reference.</summary>
    public CountryStanding StandingIn(string countryCode, DateTimeOffset at)
    {
        var normalized = Guard.Against.NullOrWhiteSpace(countryCode, nameof(countryCode))
            .Trim().ToUpperInvariant();

        var existing = _countries.FirstOrDefault(standing =>
            string.Equals(standing.CountryCode, normalized, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            return existing;
        }

        var created = CountryStanding.Create(Id, normalized, at);
        _countries.Add(created);
        return created;
    }

    /// <summary>
    /// Records that the member has left a country, if they have a standing there to close.
    /// <para>
    /// Unlike <see cref="StandingIn"/> this does not create one. A departure from a country the
    /// member never had standing in is not news, and materialising an empty standing to record
    /// it would put a country on their trust centre that they only ever left.
    /// </para>
    /// </summary>
    /// <returns>Whether a standing was closed.</returns>
    public bool RecordDeparture(string countryCode, DateOnly departedOn, DateTimeOffset at)
    {
        var standing = _countries.FirstOrDefault(
            country => string.Equals(
                country.CountryCode, countryCode, StringComparison.OrdinalIgnoreCase));

        if (standing is null || standing.DepartedOn == departedOn)
        {
            return false;
        }

        standing.RecordDeparture(departedOn, at);
        return true;
    }

    /// <summary>Adds points to one country's standing.</summary>
    public void AddCountryPoints(string countryCode, int delta, DateTimeOffset at)
    {
        StandingIn(countryCode, at).AddPoints(delta, at);
        RederiveTier(at, "Country standing changed.");
    }

    // ── Health, violations and risk ──────────────────────────────────────

    /// <summary>Records a confirmed violation and re-derives health.</summary>
    public void RecordViolation(string reason, DateTimeOffset at)
    {
        ViolationCount++;
        RederiveHealth(reason, at);
    }

    public void RecordWarning(string reason, DateTimeOffset at)
    {
        WarningCount++;
        RederiveHealth(reason, at);
    }

    public void RecordSpamReport(DateTimeOffset at)
    {
        SpamReportCount++;
        RederiveHealth("Spam reports received.", at);
    }

    public void AppealOpened(DateTimeOffset at)
    {
        ActiveAppealCount++;
        RederiveHealth("Appeal opened.", at);
    }

    public void AppealClosed(DateTimeOffset at)
    {
        ActiveAppealCount = Math.Max(0, ActiveAppealCount - 1);
        RederiveHealth("Appeal closed.", at);
    }

    /// <summary>Suspends the member. Health is forced rather than derived.</summary>
    public void Suspend(string reason, DateTimeOffset at) => SetHealth(AccountHealth.Suspended, reason, at);

    /// <summary>Lifts a suspension and lets health fall back out of the counters.</summary>
    public void Reinstate(string reason, DateTimeOffset at)
    {
        Health = AccountHealth.Good;
        RederiveHealth(reason, at);
    }

    /// <summary>
    /// Replaces the cached assessment with a fresh one, and announces a change of level.
    /// <para>
    /// A reassessment at the same level is still stored, so <see cref="RiskAssessment.AssessedAt"/>
    /// says when the evidence was last read; only a change of level is news (RWD-14, CS-4b).
    /// </para>
    /// </summary>
    public void UpdateRisk(RiskAssessment assessment)
    {
        ArgumentNullException.ThrowIfNull(assessment);

        var previous = Risk.Level;
        Risk = assessment;

        if (assessment.Level != previous)
        {
            RaiseDomainEvent(new MemberRiskLevelChangedDomainEvent(
                Id, UserId, previous, assessment.Level, assessment.AssessedAt));
        }
    }

    /// <summary>
    /// Whether the member may publish.
    /// <para>
    /// This is the question <c>ITrustService.IsPublishingEligibleAsync</c> ultimately answers,
    /// and it is deliberately conservative: a suspended or restricted account cannot publish
    /// however much reputation it has, and an unverified one cannot either.
    /// </para>
    /// </summary>
    public bool CanPublish() =>
        Health is AccountHealth.Good or AccountHealth.Excellent
        && Tier >= TrustTier.Verified;

    /// <summary>Whether the member may moderate.</summary>
    public bool CanModerate() =>
        Health is AccountHealth.Good or AccountHealth.Excellent
        && Tier >= TrustTier.Steward;

    // ── Derivation ───────────────────────────────────────────────────────

    /// <summary>
    /// Recomputes the tier from the grants and health the profile holds.
    /// <para>
    /// The ladder is intentionally simple and evidence-driven. Nothing outside the aggregate
    /// can set <see cref="Tier"/>, so a member's tier always has a reason standing behind it
    /// that can be shown to them.
    /// </para>
    /// </summary>
    private void RederiveTier(DateTimeOffset at, string reason)
    {
        var previous = Tier;

        var isVerified = _grants.Contains(VerificationType.Diaspora)
            || _grants.Contains(VerificationType.Country)
            || _grants.Contains(VerificationType.Identity);

        var hasBasics = _grants.Contains(VerificationType.Phone)
            || _grants.Contains(VerificationType.Email);

        var isSteward = HasBadge(BadgeType.Moderator) || HasBadge(BadgeType.CommunityGuide);

        var next = Tier switch
        {
            // A suspended member holds no tier above Basic regardless of evidence.
            _ when Health == AccountHealth.Suspended => TrustTier.Untrusted,
            _ when isSteward && isVerified => TrustTier.Steward,
            _ when isVerified && HasBadge(BadgeType.TrustedContributor) => TrustTier.Trusted,
            _ when isVerified => TrustTier.Verified,
            // Held something once, holds nothing now: provisional rather than a full reset,
            // so a lapsed permit is recoverable without starting from zero.
            _ when !isVerified && _badges.Any(badge => !badge.IsActive) => TrustTier.Provisional,
            _ when hasBasics => TrustTier.Basic,
            _ => TrustTier.Untrusted
        };

        if (next == previous)
        {
            return;
        }

        Tier = next;
        TierChangedAt = at;

        RaiseDomainEvent(new TrustLevelChangedDomainEvent(Id, UserId, previous, next, Truncate(reason)!, at));
    }

    /// <summary>
    /// Recomputes health from the counters.
    /// <para>
    /// <b>Violations no longer restrict or suspend the account (7 Nov 2026).</b> One violation used to
    /// restrict the whole account and three suspended it for good, with nothing to lower the count:
    /// one moderator, deciding one case three times, could end a member. The owner's strike ladder
    /// replaces it (Strike, IPostingStanding): a warning, then a week without posting in one area,
    /// then a month, then a suspension two staff must confirm, and strikes expire. A violation still
    /// counts against "excellent" standing, nothing more.
    /// </para>
    /// </summary>
    private void RederiveHealth(string reason, DateTimeOffset at)
    {
        if (Health == AccountHealth.Suspended)
        {
            return; // only Reinstate lifts a suspension
        }

        var next = this switch
        {
            { SpamReportCount: >= 3 } => AccountHealth.Restricted,
            { ActiveAppealCount: > 0 } => AccountHealth.UnderReview,
            { WarningCount: >= 2 } => AccountHealth.UnderReview,
            { ViolationCount: 0, WarningCount: 0, SpamReportCount: 0 } => AccountHealth.Excellent,
            _ => AccountHealth.Good
        };

        SetHealth(next, reason, at);
    }

    private void SetHealth(AccountHealth next, string reason, DateTimeOffset at)
    {
        HealthReasons = Truncate(reason);

        if (next == Health)
        {
            return;
        }

        var previous = Health;
        Health = next;

        RaiseDomainEvent(new AccountHealthChangedDomainEvent(Id, UserId, previous, next, at));

        // Losing health can cost a tier — a suspended member is not Verified.
        RederiveTier(at, reason);
    }

    private static IEnumerable<BadgeType> BadgesImpliedBy(VerificationType type) => type switch
    {
        VerificationType.Phone => [BadgeType.PhoneVerified],
        VerificationType.Email => [BadgeType.EmailVerified],
        VerificationType.Identity => [BadgeType.GovernmentIdVerified],
        VerificationType.Diaspora => [BadgeType.VerifiedMember],
        VerificationType.Business => [BadgeType.VerifiedBusiness],
        VerificationType.Organization => [BadgeType.VerifiedOrganization],
        VerificationType.Professional or VerificationType.Employment => [BadgeType.ProviderVerified],
        VerificationType.CommunityGuide => [BadgeType.CommunityGuide],
        VerificationType.Contributor => [BadgeType.TrustedContributor],
        _ => []
    };

    private static string? Truncate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= MaxReasonLength ? trimmed : trimmed[..MaxReasonLength];
    }

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }
}
