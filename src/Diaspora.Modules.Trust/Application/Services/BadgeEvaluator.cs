using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Extensibility;

namespace Diaspora.Modules.Trust.Application.Services;

/// <summary>
/// Runs every badge rule against a member and reconciles the result with what they hold.
/// <para>
/// Note what this class does <em>not</em> do: it never names a badge. It asks the rules in
/// <see cref="BadgeRuleCatalog"/> and tells the profile what they said. That is what makes
/// the brief's "badges must be extensible through contributor interfaces" true rather than
/// aspirational — adding <c>TrustedSeller</c> means writing one <see cref="IBadgeRule"/> and
/// changing nothing here.
/// </para>
/// <para>
/// Reconciliation runs in both directions on purpose. A rule that stops being satisfied
/// revokes its badge — a suspended moderator loses the moderator badge without anyone
/// remembering to take it away.
/// </para>
/// </summary>
public sealed class BadgeEvaluator
{
    private readonly BadgeRuleCatalog _rules;

    public BadgeEvaluator(BadgeRuleCatalog rules)
    {
        _rules = rules;
    }

    /// <summary>
    /// Brings a member's badges into line with what the rules say they have earned.
    /// <para>
    /// Mutates the profile through its own <c>AwardBadge</c>/<c>RevokeBadge</c> methods, so
    /// every change still raises the events and respects the idempotency the aggregate
    /// enforces. Returns the badges that actually changed.
    /// </para>
    /// </summary>
    public BadgeReconciliation Reconcile(
        TrustProfile profile,
        ReputationProfile reputation,
        bool isModerator,
        DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(reputation);

        var awarded = new List<(BadgeType Badge, string? Country)>();
        var revoked = new List<(BadgeType Badge, string? Country)>();

        // Global rules — evaluated once for the member.
        var globalContext = BuildContext(profile, reputation, isModerator, null, asOf);

        foreach (var rule in _rules.Global)
        {
            Apply(profile, rule, globalContext, null, asOf, awarded, revoked);
        }

        // Country-scoped rules — evaluated once per country the member has standing in.
        foreach (var standing in profile.Countries)
        {
            var countryContext = BuildContext(
                profile, reputation, isModerator, standing, asOf);

            foreach (var rule in _rules.CountryScoped)
            {
                Apply(profile, rule, countryContext, standing.CountryCode, asOf, awarded, revoked);
            }
        }

        return new BadgeReconciliation
        {
            Awarded = awarded,
            Revoked = revoked
        };
    }

    private static void Apply(
        TrustProfile profile,
        IBadgeRule rule,
        BadgeEvaluationContext context,
        string? countryCode,
        DateTimeOffset asOf,
        List<(BadgeType, string?)> awarded,
        List<(BadgeType, string?)> revoked)
    {
        var earned = rule.IsEarned(context);
        var held = profile.HasBadge(rule.Badge, countryCode);

        if (earned && !held)
        {
            profile.AwardBadge(rule.Badge, countryCode, rule.GetType().Name, asOf);
            awarded.Add((rule.Badge, countryCode));
        }
        else if (!earned && held)
        {
            profile.RevokeBadge(
                rule.Badge, countryCode, "No longer meets the criteria for this badge.", asOf);
            revoked.Add((rule.Badge, countryCode));
        }
    }

    private static BadgeEvaluationContext BuildContext(
        TrustProfile profile,
        ReputationProfile reputation,
        bool isModerator,
        CountryStanding? standing,
        DateTimeOffset asOf) => new()
        {
            Tier = profile.Tier,
            Health = profile.Health,
            Grants = profile.Grants,
            ReputationLevel = reputation.Level,
            CombinedScore = reputation.CombinedScore,
            ContributionScore = reputation.ContributionScore,
            VerificationScore = reputation.VerificationScore,
            TrustScore = reputation.TrustScore,
            GuidesPublished = reputation.GuidesPublished,
            GuidesApproved = reputation.GuidesApproved,
            HelpfulVotesReceived = reputation.HelpfulVotesReceived,
            ViolationCount = reputation.ViolationCount,
            NoViolationStreakDays = reputation.NoViolationStreakDays(asOf),
            JoinedAt = profile.JoinedAt,
            CountryCode = standing?.CountryCode,
            CountryPoints = standing?.Points ?? 0,
            CountryVerified = standing?.IsVerified ?? false,
            CountryYearsOfResidence = standing?.YearsOfResidence(DateOnly.FromDateTime(asOf.UtcDateTime)) ?? 0,
            IsModerator = isModerator,
            AsOf = asOf
        };
}

/// <summary>What a reconciliation pass changed.</summary>
public sealed record BadgeReconciliation
{
    public required IReadOnlyList<(BadgeType Badge, string? CountryCode)> Awarded { get; init; }

    public required IReadOnlyList<(BadgeType Badge, string? CountryCode)> Revoked { get; init; }

    public bool AnyChanges => Awarded.Count > 0 || Revoked.Count > 0;
}
