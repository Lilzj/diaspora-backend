using Diaspora.Modules.Trust.Domain;

namespace Diaspora.Modules.Trust.Extensibility;

/// <summary>
/// Supplies additional reputation scoring rules.
/// <para>
/// A module that creates something scoreable declares what it is worth. Jobs decides what
/// filling a vacancy earns; Trust should not have an opinion about that, and hard-coding one
/// would mean every module's scoring lives in a file no module owner can edit.
/// </para>
/// <para>
/// Two contributors declaring the same <c>ActionType</c> fail at startup. Silent
/// last-one-wins would let one module re-price another's action by load order.
/// </para>
/// </summary>
public interface IReputationRuleContributor
{
    /// <summary>The declaring module's name, for the startup diagnostic.</summary>
    string Module { get; }

    IEnumerable<ReputationRule> GetRules();
}

/// <summary>
/// Supplies additional capability requirements.
/// <para>
/// The seam that lets a module say what its own capability costs in trust —
/// "posting a job needs identity verification and 300 points" — without that threshold
/// living in Trust, which cannot reason about what a job is worth.
/// </para>
/// </summary>
public interface ITrustRequirementContributor
{
    string Module { get; }

    IEnumerable<TrustRequirement> GetRequirements();
}

/// <summary>
/// Computes one part of a member's reputation.
/// <para>
/// Distinct from <see cref="IReputationRuleContributor"/>, which prices discrete actions.
/// A calculator derives a score from state — "your marketplace rating is the mean of your
/// last fifty transactions" — which no per-action rule can express.
/// </para>
/// <para>
/// Calculators run on read, and their output is additive to the stored dimension scores. A
/// calculator that returns a different number for the same input on two consecutive calls
/// will make a member's reputation appear to flicker; they are expected to be deterministic
/// as at <see cref="ReputationCalculationContext.AsOf"/>.
/// </para>
/// </summary>
public interface IReputationCalculator
{
    string Module { get; }

    /// <summary>Which dimension this calculator's points land on.</summary>
    ReputationDimension Dimension { get; }

    /// <summary>Points to add. May be negative.</summary>
    int Calculate(ReputationCalculationContext context);
}

/// <summary>What a reputation calculator may look at.</summary>
public sealed record ReputationCalculationContext
{
    public required Guid UserId { get; init; }

    public required int ContributionScore { get; init; }

    public required int VerificationScore { get; init; }

    public required int TrustScore { get; init; }

    public required ReputationLevel Level { get; init; }

    public required int ViolationCount { get; init; }

    public string? CountryCode { get; init; }

    public required DateTimeOffset AsOf { get; init; }
}

/// <summary>
/// An extra condition on a capability, beyond the declarative
/// <see cref="TrustRequirement"/> thresholds.
/// <para>
/// Thresholds cover "enough points, long enough resident, verified". They cannot express
/// "not while a fraud investigation is open on this member" or "not from a country we have
/// suspended onboarding for". Those are rules, and this is where they go.
/// </para>
/// <para>
/// Every applicable rule must pass. A rule may not grant — only refuse — because a rule that
/// could override a threshold would make the thresholds advisory.
/// </para>
/// </summary>
public interface ITrustRule
{
    string Module { get; }

    /// <summary>
    /// Capability keys this rule applies to. Empty means every capability.
    /// </summary>
    IReadOnlyCollection<string> AppliesTo { get; }

    /// <summary>Order within the rule set. Lower runs first; first refusal wins.</summary>
    int Order => 0;

    /// <summary>
    /// Whether the action may proceed. Returning a refusal short-circuits the remaining
    /// rules, so the member sees the most important reason rather than the last one.
    /// </summary>
    TrustRuleVerdict Evaluate(TrustRuleContext context);
}

/// <summary>What a trust rule may look at.</summary>
public sealed record TrustRuleContext
{
    public required Guid UserId { get; init; }

    public required string CapabilityKey { get; init; }

    public required TrustTier Tier { get; init; }

    public required AccountHealth Health { get; init; }

    public required IReadOnlyCollection<VerificationType> Grants { get; init; }

    public required int CombinedScore { get; init; }

    public required int ViolationCount { get; init; }

    public required RiskLevel RiskLevel { get; init; }

    /// <summary>The country the action targets, when it is country-scoped.</summary>
    public string? TargetCountry { get; init; }

    public int CountryPoints { get; init; }

    public bool CountryVerified { get; init; }

    public int CountryYearsOfResidence { get; init; }

    public required DateTimeOffset AsOf { get; init; }
}

/// <summary>A trust rule's answer.</summary>
public sealed record TrustRuleVerdict
{
    public required bool Allowed { get; init; }

    /// <summary>The code the client branches on. Ignored when allowed.</summary>
    public EligibilityCode Code { get; init; } = EligibilityCode.ALLOW;

    /// <summary>Shown to the member. Required when refusing.</summary>
    public string? Reason { get; init; }

    /// <summary>Named requirements the member has not met, for the progress meter.</summary>
    public IReadOnlyCollection<string> UnmetRequirements { get; init; } = [];

    /// <summary>Seconds to wait, when the refusal is a rate limit or cooldown.</summary>
    public int? WaitTimeSeconds { get; init; }

    public static TrustRuleVerdict Allow { get; } = new()
    {
        Allowed = true,
        Code = EligibilityCode.ALLOW
    };

    public static TrustRuleVerdict Deny(
        string reason,
        EligibilityCode code = EligibilityCode.DENIED,
        IReadOnlyCollection<string>? unmet = null,
        int? waitSeconds = null) => new()
        {
            Allowed = false,
            Code = code,
            Reason = reason,
            UnmetRequirements = unmet ?? [],
            WaitTimeSeconds = waitSeconds
        };
}
