using Diaspora.Modules.Trust.Domain;

namespace Diaspora.Modules.Trust.Extensibility;

/// <summary>
/// A rule deciding whether a member has earned one badge.
/// <para>
/// One rule per badge, evaluated after anything that could change a member's standing. The
/// brief asks for badges to be "extensible through contributor interfaces" — this is that
/// interface, and it is why <c>TopContributor</c> and a Marketplace module's future
/// <c>TrustedSeller</c> are added the same way: a new class, no edit.
/// </para>
/// <para>
/// A rule is asked, never told. It cannot award the badge itself — it returns a verdict and
/// <see cref="Domain.TrustProfile.AwardBadge"/> does the awarding, so every badge on the
/// platform is granted through one code path that raises one event.
/// </para>
/// </summary>
public interface IBadgeRule
{
    /// <summary>The badge this rule governs.</summary>
    BadgeType Badge { get; }

    /// <summary>
    /// Whether the badge is scoped to a country. Country-scoped rules are evaluated once per
    /// country the member has standing in; global rules once per member.
    /// </summary>
    bool IsCountryScoped { get; }

    /// <summary>
    /// Whether the member has earned it.
    /// <para>
    /// Must be a pure function of the context. A rule that reaches for a database or a clock
    /// of its own makes badge awards unreproducible, and "why did I lose this badge?" becomes
    /// unanswerable.
    /// </para>
    /// </summary>
    bool IsEarned(BadgeEvaluationContext context);
}

/// <summary>
/// Everything a badge rule is allowed to look at.
/// <para>
/// Deliberately a snapshot rather than the live aggregates: a rule holding a
/// <c>TrustProfile</c> could mutate it, and the whole point of the seam is that rules decide
/// and the aggregate acts.
/// </para>
/// </summary>
public sealed record BadgeEvaluationContext
{
    public required TrustTier Tier { get; init; }

    public required AccountHealth Health { get; init; }

    /// <summary>Verification types the member currently holds.</summary>
    public required IReadOnlyCollection<VerificationType> Grants { get; init; }

    public required ReputationLevel ReputationLevel { get; init; }

    public required int CombinedScore { get; init; }

    public required int ContributionScore { get; init; }

    public required int VerificationScore { get; init; }

    public required int TrustScore { get; init; }

    public required int GuidesPublished { get; init; }

    public required int GuidesApproved { get; init; }

    public required int HelpfulVotesReceived { get; init; }

    public required int ViolationCount { get; init; }

    public required int NoViolationStreakDays { get; init; }

    /// <summary>When the member joined. Drives the Early Member badge.</summary>
    public required DateTimeOffset JoinedAt { get; init; }

    /// <summary>Set when the rule is country-scoped; null for a global rule.</summary>
    public string? CountryCode { get; init; }

    /// <summary>The member's points in <see cref="CountryCode"/>, when country-scoped.</summary>
    public int CountryPoints { get; init; }

    /// <summary>Whether residency in <see cref="CountryCode"/> is verified.</summary>
    public bool CountryVerified { get; init; }

    /// <summary>Complete years of verified residence in <see cref="CountryCode"/>.</summary>
    public int CountryYearsOfResidence { get; init; }

    /// <summary>Whether the member holds a moderator mandate.</summary>
    public required bool IsModerator { get; init; }

    public required DateTimeOffset AsOf { get; init; }
}
