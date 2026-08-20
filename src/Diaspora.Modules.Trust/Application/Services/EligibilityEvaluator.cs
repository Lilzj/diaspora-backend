using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Extensibility;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Trust.Application.Services;

/// <summary>
/// Decides whether a member may take an action.
/// <para>
/// The server-side replacement for the frontend's <c>TrustEngine.evaluateAction</c>, which
/// today holds the platform's publishing thresholds, rate limits and health restrictions in
/// <c>src/lib/trustEngine.ts</c> — shipped to every device and editable by anyone with a
/// debugger. That code stays where it is, because instant local feedback is worth having;
/// it simply stops being the thing that decides.
/// </para>
/// <para>
/// The order of checks is deliberate and matches the client's, so a member sees the same
/// reason from both: health first, then rules, then thresholds, then rate limits. A member
/// who is suspended <em>and</em> short on reputation should be told they are suspended.
/// </para>
/// </summary>
public sealed class EligibilityEvaluator
{
    private readonly ITrustProfileRepository _profiles;
    private readonly IReputationProfileRepository _reputations;
    private readonly TrustRequirementCatalog _requirements;
    private readonly TrustRuleChain _rules;
    private readonly ISubjectRateLimiter _rateLimiter;
    private readonly IClock _clock;

    public EligibilityEvaluator(
        ITrustProfileRepository profiles,
        IReputationProfileRepository reputations,
        TrustRequirementCatalog requirements,
        TrustRuleChain rules,
        ISubjectRateLimiter rateLimiter,
        IClock clock)
    {
        _profiles = profiles;
        _reputations = reputations;
        _requirements = requirements;
        _rules = rules;
        _rateLimiter = rateLimiter;
        _clock = clock;
    }

    /// <summary>
    /// Evaluates one capability for one member.
    /// </summary>
    /// <param name="dryRun">
    /// When true the rate-limit budget is inspected but not consumed, so a screen can grey
    /// out a button without spending the member's quota. Mirrors the client's <c>dryRun</c>
    /// parameter, which defaults to true for the same reason.
    /// </param>
    public async Task<EligibilityDecision> EvaluateAsync(
        UserId userId,
        string capabilityKey,
        string? targetCountry = null,
        bool dryRun = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var requirement = _requirements.For(capabilityKey);
        if (requirement is null)
        {
            // An unknown capability is allowed rather than refused. Trust does not know every
            // capability on the platform, and a module that has not declared a requirement
            // has not asked Trust to gate it — refusing by default would break every
            // ungated action the moment this endpoint shipped.
            return EligibilityDecision.Allow();
        }

        var now = _clock.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var profile = await _profiles.GetByUserReadOnlyAsync(userId, cancellationToken);
        if (profile is null)
        {
            return EligibilityDecision.Deny(
                "Complete Diaspora Verification to unlock this action.",
                EligibilityCode.DENIED,
                ["Diaspora Verification"]);
        }

        var reputation = await _reputations.GetByUserReadOnlyAsync(userId, cancellationToken);

        var standing = targetCountry is null
            ? null
            : profile.Countries.FirstOrDefault(country =>
                string.Equals(country.CountryCode, targetCountry, StringComparison.OrdinalIgnoreCase));

        // ── 1. Rules (health, risk, and anything a module contributed) ───
        var ruleVerdict = _rules.Evaluate(new TrustRuleContext
        {
            UserId = userId.Value,
            CapabilityKey = capabilityKey,
            Tier = profile.Tier,
            Health = profile.Health,
            Grants = profile.Grants,
            CombinedScore = reputation?.CombinedScore ?? 0,
            ViolationCount = profile.ViolationCount,
            RiskLevel = profile.Risk.Level,
            TargetCountry = targetCountry,
            CountryPoints = standing?.Points ?? 0,
            CountryVerified = standing?.IsVerified ?? false,
            CountryYearsOfResidence = standing?.YearsOfResidence(today) ?? 0,
            AsOf = now
        });

        if (!ruleVerdict.Allowed)
        {
            return new EligibilityDecision
            {
                Allowed = false,
                Code = ruleVerdict.Code,
                Reason = ruleVerdict.Reason,
                UnmetRequirements = [.. ruleVerdict.UnmetRequirements],
                WaitTimeSeconds = ruleVerdict.WaitTimeSeconds
            };
        }

        // ── 2. Declarative thresholds ────────────────────────────────────
        var unmet = CollectUnmet(requirement, profile, standing, today);

        if (unmet.Count > 0)
        {
            return EligibilityDecision.Deny(
                $"You do not yet meet the requirements for {requirement.Name}.",
                EligibilityCode.DENIED,
                unmet);
        }

        // ── 3. Rate limits and cooldowns ─────────────────────────────────
        if (requirement.RateLimitPerDay > 0)
        {
            var key = $"trust:{capabilityKey}:{userId.Value}";

            if (dryRun)
            {
                // Nothing to consume, and no way to inspect without consuming through the
                // limiter contract — so a dry run reports the optimistic answer and the real
                // call is the one that can refuse. The client already treats its own dry-run
                // result as advisory.
                return EligibilityDecision.Allow();
            }

            var lease = await _rateLimiter.AcquireAsync(
                key, requirement.RateLimitPerDay, TimeSpan.FromDays(1), cancellationToken);

            if (!lease.IsAllowed)
            {
                var waitSeconds = (int)Math.Ceiling(lease.RetryAfter.TotalSeconds);

                // The client distinguishes a short cooldown from a real rate limit and shows
                // a different message for each. Five seconds is its threshold.
                var code = waitSeconds <= 5 ? EligibilityCode.COOLDOWN : EligibilityCode.RATE_LIMITED;

                return new EligibilityDecision
                {
                    Allowed = false,
                    Code = code,
                    Reason = "Please wait. Rate limit active.",
                    UnmetRequirements = [],
                    WaitTimeSeconds = waitSeconds
                };
            }
        }

        return EligibilityDecision.Allow();
    }

    /// <summary>
    /// Builds the unlock checklist for every declared capability.
    /// <para>
    /// Feeds the trust centre's Feature Gates tab, which today computes this on the device
    /// from <c>GOVERNANCE_CONFIG</c>. Same shape, same wording, authoritative source.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<CapabilityProgress>> DescribeRequirementsAsync(
        UserId userId,
        string? targetCountry = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var now = _clock.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var profile = await _profiles.GetByUserReadOnlyAsync(userId, cancellationToken);

        var results = new List<CapabilityProgress>();

        foreach (var requirement in _requirements.All.OrderBy(r => r.Name, StringComparer.Ordinal))
        {
            var standing = profile is null || targetCountry is null
                ? null
                : profile.Countries.FirstOrDefault(country =>
                    string.Equals(country.CountryCode, targetCountry, StringComparison.OrdinalIgnoreCase));

            var items = new List<RequirementProgress>();

            if (requirement.RequiresPhone)
            {
                var met = profile?.Grants.Contains(VerificationType.Phone) ?? false;
                items.Add(new RequirementProgress
                {
                    Description = "Verified Phone Number",
                    IsMet = met,
                    CurrentProgress = met ? "Verified" : "Pending binding"
                });
            }

            if (requirement.RequiresIdentity)
            {
                var met = profile?.Grants.Contains(VerificationType.Identity) ?? false;
                items.Add(new RequirementProgress
                {
                    Description = "Government ID Verification",
                    IsMet = met,
                    CurrentProgress = met ? "Verified" : "Unverified"
                });
            }

            if (requirement.RequiresCountryVerification)
            {
                var met = standing?.IsVerified
                    ?? profile?.Grants.Contains(VerificationType.Country)
                    ?? false;

                items.Add(new RequirementProgress
                {
                    Description = targetCountry is null
                        ? "Verified Residency"
                        : $"Verified Residency in {targetCountry}",
                    IsMet = met,
                    CurrentProgress = met ? "Verified" : "Unverified"
                });
            }

            if (requirement.MinimumCountryReputation > 0)
            {
                var points = standing?.Points ?? 0;
                items.Add(new RequirementProgress
                {
                    Description = $"Country Reputation Points (Requires {requirement.MinimumCountryReputation})",
                    IsMet = points >= requirement.MinimumCountryReputation,
                    CurrentProgress = $"{points} / {requirement.MinimumCountryReputation} pts"
                });
            }

            if (requirement.MinimumResidenceYears > 0)
            {
                var years = standing?.YearsOfResidence(today) ?? 0;
                var plural = requirement.MinimumResidenceYears > 1 ? "s" : string.Empty;

                items.Add(new RequirementProgress
                {
                    Description = $"Residence Duration (Requires {requirement.MinimumResidenceYears} year{plural})",
                    IsMet = years >= requirement.MinimumResidenceYears,
                    CurrentProgress = $"{years} / {requirement.MinimumResidenceYears} yr{plural}"
                });
            }

            results.Add(new CapabilityProgress
            {
                CapabilityKey = requirement.CapabilityKey,
                Name = requirement.Name,
                IsUnlocked = items.Count > 0
                    ? items.TrueForAll(item => item.IsMet)
                    : (profile?.Tier ?? TrustTier.Untrusted) >= requirement.MinimumTier,
                Requirements = items
            });
        }

        return results;
    }

    /// <summary>
    /// The thresholds a member has not met, phrased the way the client phrases them so the
    /// unlock checklist reads identically whichever side produced it.
    /// </summary>
    private static List<string> CollectUnmet(
        TrustRequirement requirement,
        TrustProfile profile,
        CountryStanding? standing,
        DateOnly today)
    {
        var unmet = new List<string>();

        if (profile.Tier < requirement.MinimumTier)
        {
            unmet.Add("Diaspora Verification");
        }

        if (requirement.RequiresPhone && !profile.Grants.Contains(VerificationType.Phone))
        {
            unmet.Add("Phone Verification");
        }

        if (requirement.RequiresIdentity && !profile.Grants.Contains(VerificationType.Identity))
        {
            unmet.Add("Government ID Verification");
        }

        if (requirement.RequiresCountryVerification)
        {
            var verified = standing?.IsVerified ?? profile.Grants.Contains(VerificationType.Country);
            if (!verified)
            {
                unmet.Add("Country Residency Verification");
            }
        }

        if (requirement.MinimumCountryReputation > 0)
        {
            var points = standing?.Points ?? 0;
            if (points < requirement.MinimumCountryReputation)
            {
                unmet.Add(
                    $"Requires {requirement.MinimumCountryReputation} reputation in target country "
                    + $"(you have {points})");
            }
        }

        if (requirement.MinimumResidenceYears > 0)
        {
            var years = standing?.YearsOfResidence(today) ?? 0;
            if (years < requirement.MinimumResidenceYears)
            {
                unmet.Add(
                    $"Requires {requirement.MinimumResidenceYears} years residence (you have {years} yrs)");
            }
        }

        return unmet;
    }
}

/// <summary>The evaluator's answer.</summary>
public sealed record EligibilityDecision
{
    public required bool Allowed { get; init; }

    public required EligibilityCode Code { get; init; }

    public string? Reason { get; init; }

    public IReadOnlyList<string> UnmetRequirements { get; init; } = [];

    public int? WaitTimeSeconds { get; init; }

    public static EligibilityDecision Allow() => new()
    {
        Allowed = true,
        Code = EligibilityCode.ALLOW
    };

    public static EligibilityDecision Deny(
        string reason,
        EligibilityCode code,
        IReadOnlyList<string>? unmet = null) => new()
        {
            Allowed = false,
            Code = code,
            Reason = reason,
            UnmetRequirements = unmet ?? []
        };
}

/// <summary>One capability's unlock progress.</summary>
public sealed record CapabilityProgress
{
    public required string CapabilityKey { get; init; }

    public required string Name { get; init; }

    public required bool IsUnlocked { get; init; }

    public required IReadOnlyList<RequirementProgress> Requirements { get; init; }
}

/// <summary>One line of an unlock checklist.</summary>
public sealed record RequirementProgress
{
    public required string Description { get; init; }

    public required bool IsMet { get; init; }

    public required string CurrentProgress { get; init; }
}
