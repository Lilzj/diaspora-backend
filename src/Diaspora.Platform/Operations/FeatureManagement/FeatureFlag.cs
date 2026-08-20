namespace Diaspora.Platform.Operations.FeatureManagement;

/// <summary>What a rollout rule matches on.</summary>
public enum RolloutDimension
{
    /// <summary>Everyone. The rule's only question is on or off.</summary>
    Everyone = 0,

    /// <summary>Members in named countries.</summary>
    Country = 1,

    /// <summary>Named tenants.</summary>
    Tenant = 2,

    /// <summary>Members holding a named role.</summary>
    Role = 3,

    /// <summary>Named members. The allowlist that gets a feature to the people testing it.</summary>
    User = 4,

    /// <summary>Named environments.</summary>
    Environment = 5,

    /// <summary>
    /// A stable percentage of subjects, hashed on the subject id. Sticky by construction: a member
    /// either is or is not in the bucket, and stays there across requests and restarts.
    /// </summary>
    Percentage = 6
}

/// <summary>
/// One rule in a flag's rollout.
/// </summary>
/// <param name="Dimension">What it matches on.</param>
/// <param name="Values">
/// The values that match — country codes, role names, member ids. Ignored for
/// <see cref="RolloutDimension.Everyone"/> and <see cref="RolloutDimension.Percentage"/>.
/// </param>
/// <param name="Percentage">The bucket size, 0–100, for a percentage rule.</param>
/// <param name="Enabled">
/// What a match means. <see langword="false"/> is a deliberate exclusion, which is what makes
/// "everyone except this country" expressible.
/// </param>
public sealed record RolloutRule(
    RolloutDimension Dimension,
    IReadOnlyList<string> Values,
    int Percentage = 0,
    bool Enabled = true)
{
    public static RolloutRule Everyone(bool enabled = true) => new(RolloutDimension.Everyone, [], 0, enabled);

    public static RolloutRule ForCountries(params string[] countries) =>
        new(RolloutDimension.Country, countries);

    public static RolloutRule ForRoles(params string[] roles) => new(RolloutDimension.Role, roles);

    public static RolloutRule ForUsers(params string[] userIds) => new(RolloutDimension.User, userIds);

    public static RolloutRule ForEnvironments(params string[] environments) =>
        new(RolloutDimension.Environment, environments);

    public static RolloutRule ForPercentage(int percentage) =>
        new(RolloutDimension.Percentage, [], percentage);
}

/// <summary>
/// A feature flag: what it guards, who currently sees it, and under which policy.
/// <para>
/// <b>Not the same thing as a feature gate, and the distinction is worth keeping straight.</b> A
/// feature <em>gate</em> (<c>IFeatureGateService</c>, B2.6) answers "is this member entitled to this
/// capability" — a progression and permission question whose answer belongs to the member. A feature
/// <em>flag</em> answers "is this code path turned on for this deployment right now" — an operational
/// question whose answer belongs to whoever is running the platform. A gate that changed hourly would
/// be a broken entitlement model; a flag that could not would be useless.
/// </para>
/// </summary>
/// <param name="Name">Dotted, lower case, namespaced to the owning module.</param>
/// <param name="Module">Who owns it.</param>
/// <param name="Description">What it guards. Read by whoever is deciding whether to turn it off at 3am.</param>
/// <param name="DefaultState">The answer when no rule matches.</param>
/// <param name="Rules">Evaluated in order. The first match wins.</param>
/// <param name="PolicyName">The <c>FeatureFlagPolicy</c> governing evaluation.</param>
/// <param name="CreatedAt">When it was first declared. Drives the staleness report.</param>
/// <param name="UpdatedAt">When it last changed.</param>
/// <param name="UpdatedBy">Who last changed it.</param>
public sealed record FeatureFlag(
    string Name,
    string Module,
    string Description,
    bool DefaultState,
    IReadOnlyList<RolloutRule> Rules,
    string PolicyName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? UpdatedBy);

/// <summary>What is known about the caller when a flag is evaluated.</summary>
/// <param name="UserId">The member, when there is one.</param>
/// <param name="TenantId">The tenant.</param>
/// <param name="CountryCode">ISO country.</param>
/// <param name="Roles">The caller's roles.</param>
/// <param name="Environment">The deployment environment.</param>
public readonly record struct FeatureFlagContext(
    string? UserId = null,
    string? TenantId = null,
    string? CountryCode = null,
    IReadOnlyList<string>? Roles = null,
    string? Environment = null);

/// <summary>Why a flag resolved the way it did.</summary>
/// <param name="Flag">The flag's name.</param>
/// <param name="IsEnabled">The answer.</param>
/// <param name="Reason">Which rule decided, or why none did.</param>
/// <param name="MatchedDimension">The dimension that matched, if one did.</param>
public readonly record struct FeatureFlagEvaluation(
    string Flag,
    bool IsEnabled,
    string Reason,
    RolloutDimension? MatchedDimension);

/// <summary>One change to a flag, for the audit trail.</summary>
/// <param name="Flag">Which flag.</param>
/// <param name="ChangedAt">When.</param>
/// <param name="ChangedBy">Who. Null means the platform itself — a declaration at startup.</param>
/// <param name="Before">The flag as it was, or null when it was created.</param>
/// <param name="After">The flag as it is, or null when it was removed.</param>
/// <param name="Reason">Why, when the caller supplied one.</param>
public sealed record FeatureFlagChange(
    string Flag,
    DateTimeOffset ChangedAt,
    string? ChangedBy,
    FeatureFlag? Before,
    FeatureFlag? After,
    string? Reason);
