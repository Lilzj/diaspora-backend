namespace Diaspora.Contracts.Authorization;

/// <summary>
/// Why a feature gate is closed for a particular subject.
/// <para>
/// A code rather than a sentence. The catalogue decides <em>whether</em> a gate is open
/// and <em>why</em> it is not; the module that owns the member experience decides how to
/// say so, in the member's language. Putting English in the kernel would make every
/// translation a platform change.
/// </para>
/// </summary>
public enum FeatureBlockCode
{
    /// <summary>The account is suspended, locked or closed.</summary>
    AccountRestricted = 0,

    /// <summary>The member has not finished onboarding.</summary>
    RequiresOnboarding = 1,

    /// <summary>The member needs to reach a higher tier — usually verification.</summary>
    RequiresHigherTier = 2,

    /// <summary>Staff-only capability.</summary>
    StaffOnly = 3,

    /// <summary>
    /// The member has moved past the tier band in which this applies — for example
    /// submitting a verification request when one is already in flight.
    /// </summary>
    NoLongerApplicable = 4
}

/// <summary>One closed gate and the reason it is closed.</summary>
/// <param name="Feature">The gate name.</param>
/// <param name="Code">Why it is closed.</param>
/// <param name="UnlocksAtTier">
/// The tier that would open it, when progressing would. <c>null</c> when no amount of
/// progress helps — a staff gate, or a capability the member has already moved past.
/// </param>
public sealed record BlockedFeature(string Feature, FeatureBlockCode Code, AccessTier? UnlocksAtTier);

/// <summary>The full open/closed picture for one subject.</summary>
/// <param name="Granted">Gate names that are open.</param>
/// <param name="Blocked">Closed gates with their reasons.</param>
public sealed record FeatureEvaluation(
    IReadOnlyList<string> Granted,
    IReadOnlyList<BlockedFeature> Blocked)
{
    public static readonly FeatureEvaluation Empty = new([], []);

    public bool IsGranted(string featureName) =>
        Granted.Contains(featureName, StringComparer.Ordinal);
}
