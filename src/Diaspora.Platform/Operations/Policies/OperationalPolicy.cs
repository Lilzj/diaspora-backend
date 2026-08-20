namespace Diaspora.Platform.Operations.Policies;

/// <summary>
/// A named, versioned operational rule.
/// <para>
/// Every operational decision the platform makes at runtime — how many times to retry, when to
/// open a breaker, how long a cache entry lives, what counts as a healthy module — is expressed
/// as one of these rather than as a constant inside the component that acts on it. That is the
/// whole point of Part 12: an operator changing retry behaviour should not need a code change,
/// and an incident review should be able to name the exact policy version that was in force.
/// </para>
/// <para>
/// <b>Versioned, and the version is load-bearing.</b> Two declarations of one policy name are a
/// conflict at the same version and an override at a higher one. That is how a deployment
/// replaces a platform default without the platform knowing the deployment exists, and it is why
/// <see cref="OperationalPolicyCatalog"/> refuses a same-version duplicate instead of picking a
/// winner: a silent winner is how two components end up believing different things about one name.
/// </para>
/// <para>
/// <b>Business modules never see these.</b> A policy is consumed by the platform component that
/// enforces it — the resilience executor, the cache, the limiter — never by a command handler. The
/// architecture suite pins that: <c>No_business_module_references_the_operations_assembly</c>.
/// </para>
/// </summary>
public abstract record OperationalPolicy
{
    /// <summary>
    /// The separator every policy name uses. A name is <c>scope.concern</c> — <c>outbox.dispatch</c>,
    /// <c>financial.payment-gateway</c> — so a reader of an alert knows what it governs.
    /// </summary>
    public const char NameSeparator = '.';

    protected OperationalPolicy(string name, int version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);

        Name = name;
        Version = version;
    }

    /// <summary>The policy's identity. Stable across versions; unique within a <see cref="Kind"/>.</summary>
    public string Name { get; }

    /// <summary>
    /// Monotonic. A higher version of the same name and kind replaces a lower one at composition
    /// time, which is what makes a deployment override possible without an edit to the default.
    /// </summary>
    public int Version { get; }

    /// <summary>
    /// The policy family — <c>retry</c>, <c>circuit-breaker</c>, <c>rate-limit</c> and so on. Two
    /// families may use the same name for two different concerns without colliding, because a
    /// component asks the catalogue for a name <em>and</em> a type.
    /// </summary>
    public abstract string Kind { get; }

    /// <summary>
    /// Why this policy exists, in one line. Surfaced on the operational dashboard beside the value,
    /// because a number with no explanation is a number nobody dares change during an incident.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Self-check. Returns one message per problem and an empty sequence when the policy is sound.
    /// <para>
    /// Run at composition rather than at first use. A retry policy whose maximum delay is below its
    /// initial delay is a configuration error that should stop a rollout, not a surprise the first
    /// time a payment provider times out at 3am.
    /// </para>
    /// </summary>
    public virtual IEnumerable<string> Validate() => [];

    /// <summary>The catalogue key: a policy is identified by its family and its name together.</summary>
    public OperationalPolicyKey Key => new(Kind, Name);

    public sealed override string ToString() => $"{Kind}:{Name}@v{Version}";
}

/// <summary>
/// A policy's composite identity. A <c>retry</c> named <c>media.upload</c> and a <c>cache</c> named
/// <c>media.upload</c> are different policies, and neither shadows the other.
/// </summary>
public readonly record struct OperationalPolicyKey(string Kind, string Name)
{
    public override string ToString() => $"{Kind}:{Name}";
}
