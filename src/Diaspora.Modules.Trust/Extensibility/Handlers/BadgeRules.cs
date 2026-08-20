using Diaspora.Modules.Trust.Domain;

namespace Diaspora.Modules.Trust.Extensibility.Handlers;

/// <summary>
/// The badge rules Trust ships with.
/// <para>
/// Each is one small class implementing <see cref="IBadgeRule"/>. The shape is the point: a
/// new badge is a new file, and nothing in this one changes. The brief's "badges must be
/// extensible through contributor interfaces" is satisfied by the fact that
/// <c>BadgeEvaluator</c> never names any of these types.
/// </para>
/// </summary>
public sealed class VerifiedMemberBadgeRule : IBadgeRule
{
    public BadgeType Badge => BadgeType.VerifiedMember;

    public bool IsCountryScoped => false;

    public bool IsEarned(BadgeEvaluationContext context) =>
        context.Grants.Contains(VerificationType.Diaspora)
        && context.Health != AccountHealth.Suspended;
}

public sealed class PhoneVerifiedBadgeRule : IBadgeRule
{
    public BadgeType Badge => BadgeType.PhoneVerified;

    public bool IsCountryScoped => false;

    public bool IsEarned(BadgeEvaluationContext context) =>
        context.Grants.Contains(VerificationType.Phone);
}

public sealed class EmailVerifiedBadgeRule : IBadgeRule
{
    public BadgeType Badge => BadgeType.EmailVerified;

    public bool IsCountryScoped => false;

    public bool IsEarned(BadgeEvaluationContext context) =>
        context.Grants.Contains(VerificationType.Email);
}

public sealed class GovernmentIdVerifiedBadgeRule : IBadgeRule
{
    public BadgeType Badge => BadgeType.GovernmentIdVerified;

    public bool IsCountryScoped => false;

    public bool IsEarned(BadgeEvaluationContext context) =>
        context.Grants.Contains(VerificationType.Identity);
}

public sealed class ProviderVerifiedBadgeRule : IBadgeRule
{
    public BadgeType Badge => BadgeType.ProviderVerified;

    public bool IsCountryScoped => false;

    public bool IsEarned(BadgeEvaluationContext context) =>
        context.Grants.Contains(VerificationType.Professional)
        || context.Grants.Contains(VerificationType.Employment);
}

public sealed class VerifiedBusinessBadgeRule : IBadgeRule
{
    public BadgeType Badge => BadgeType.VerifiedBusiness;

    public bool IsCountryScoped => false;

    public bool IsEarned(BadgeEvaluationContext context) =>
        context.Grants.Contains(VerificationType.Business);
}

public sealed class VerifiedOrganizationBadgeRule : IBadgeRule
{
    public BadgeType Badge => BadgeType.VerifiedOrganization;

    public bool IsCountryScoped => false;

    public bool IsEarned(BadgeEvaluationContext context) =>
        context.Grants.Contains(VerificationType.Organization);
}

/// <summary>Country-scoped: earned once per country whose residency is verified.</summary>
public sealed class CountryVerifiedBadgeRule : IBadgeRule
{
    public BadgeType Badge => BadgeType.CountryVerified;

    public bool IsCountryScoped => true;

    public bool IsEarned(BadgeEvaluationContext context) => context.CountryVerified;
}

/// <summary>
/// Sustained contribution, not merely a high score.
/// <para>
/// Requires the level, a clean record, <em>and</em> approved guides — three signals rather
/// than one, because a member can reach TrustedContributor on verification points alone and
/// this badge is meant to say something about what they have written.
/// </para>
/// </summary>
public sealed class TrustedContributorBadgeRule : IBadgeRule
{
    public BadgeType Badge => BadgeType.TrustedContributor;

    public bool IsCountryScoped => false;

    public bool IsEarned(BadgeEvaluationContext context) =>
        context.ReputationLevel >= ReputationLevel.TrustedContributor
        && context.GuidesApproved >= 3
        && context.ViolationCount == 0
        && context.Health is AccountHealth.Good or AccountHealth.Excellent;
}

/// <summary>
/// The top of the contribution ladder. Deliberately hard: expert level, ten approved guides,
/// a hundred helpful votes and a clean year.
/// </summary>
public sealed class TopContributorBadgeRule : IBadgeRule
{
    public BadgeType Badge => BadgeType.TopContributor;

    public bool IsCountryScoped => false;

    public bool IsEarned(BadgeEvaluationContext context) =>
        context.ReputationLevel >= ReputationLevel.CommunityExpert
        && context.GuidesApproved >= 10
        && context.HelpfulVotesReceived >= 100
        && context.ViolationCount == 0
        && context.NoViolationStreakDays >= 365;
}

/// <summary>
/// A mandate, not an achievement — the guide programme grants it through a verification, so
/// the rule reads the grant rather than inferring eligibility from points.
/// </summary>
public sealed class CommunityGuideBadgeRule : IBadgeRule
{
    public BadgeType Badge => BadgeType.CommunityGuide;

    public bool IsCountryScoped => true;

    public bool IsEarned(BadgeEvaluationContext context) =>
        context.Grants.Contains(VerificationType.CommunityGuide)
        && context.CountryVerified
        && context.Health is AccountHealth.Good or AccountHealth.Excellent;
}

/// <summary>A moderator mandate. Held while the member has the role, revoked when they lose it.</summary>
public sealed class ModeratorBadgeRule : IBadgeRule
{
    public BadgeType Badge => BadgeType.Moderator;

    public bool IsCountryScoped => false;

    public bool IsEarned(BadgeEvaluationContext context) =>
        context.IsModerator && context.Health is AccountHealth.Good or AccountHealth.Excellent;
}

/// <summary>
/// Joined during the platform's first year.
/// <para>
/// The cutoff is a constant rather than configuration on purpose: it is a historical fact,
/// and a configurable one would let a deployment accidentally grant it to everyone.
/// </para>
/// </summary>
public sealed class EarlyMemberBadgeRule : IBadgeRule
{
    /// <summary>Members who joined before this date are founding members.</summary>
    public static readonly DateTimeOffset Cutoff = new(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public BadgeType Badge => BadgeType.EarlyMember;

    public bool IsCountryScoped => false;

    public bool IsEarned(BadgeEvaluationContext context) => context.JoinedAt < Cutoff;
}
