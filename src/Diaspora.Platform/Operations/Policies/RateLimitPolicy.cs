namespace Diaspora.Platform.Operations.Policies;

/// <summary>
/// What the platform partitions a rate limit on.
/// <para>
/// The dimension is the whole design. B2's transport limiter partitions on the client address,
/// which stops one noisy client and does nothing about a thousand hosts making one request each
/// at the same phone number. Naming the dimension explicitly is what lets a policy say "this
/// budget belongs to the account being attacked" rather than "to whoever is attacking it".
/// </para>
/// </summary>
public enum RateLimitDimension
{
    /// <summary>One budget for the whole deployment. The last line before a queue overruns.</summary>
    Global = 0,

    /// <summary>Per authenticated member.</summary>
    User = 1,

    /// <summary>Per tenant. Single-tenant today; the partition is here so it need not be retrofitted.</summary>
    Tenant = 2,

    /// <summary>Per route.</summary>
    Endpoint = 3,

    /// <summary>Per module, so one vertical cannot consume the platform's whole budget.</summary>
    Module = 4,

    /// <summary>Per country, which is how a regional abuse spike is contained without a global cap.</summary>
    Country = 5,

    /// <summary>Per role, so staff tooling and member traffic have separate budgets.</summary>
    Role = 6,

    /// <summary>Per client address. Still useful; no longer the only option.</summary>
    ClientAddress = 7,

    /// <summary>
    /// Per subject named by the caller — a phone number, an account, a device. The dimension that
    /// makes the cost of an attack scale with the target rather than with the attacker's address pool.
    /// </summary>
    Subject = 8
}

/// <summary>The shape of the window a limit is enforced over.</summary>
public enum RateLimitAlgorithm
{
    /// <summary>Segmented window. Smooths the double-rate burst a fixed window permits at its boundary.</summary>
    SlidingWindow = 0,

    /// <summary>Fixed window. Cheapest; bursts at the boundary.</summary>
    FixedWindow = 1,

    /// <summary>Token bucket. Allows a configured burst and refills continuously.</summary>
    TokenBucket = 2,

    /// <summary>Concurrency limit. Bounds calls in flight rather than calls per unit time.</summary>
    Concurrency = 3
}

/// <summary>
/// A rate limit, expressed once and enforced anywhere.
/// <para>
/// Closes DEBT-028: before B17 a rate limit could only be declared in the host's middleware, so a
/// module that wanted one had to ask the platform team to edit <c>Program.cs</c>. A module now
/// declares a policy through <see cref="IOperationalPolicyContributor"/> and the limiter finds it.
/// </para>
/// </summary>
public sealed record RateLimitPolicy : OperationalPolicy
{
    public const string PolicyKind = "rate-limit";

    public RateLimitPolicy(string name, int version = 1) : base(name, version)
    {
    }

    public override string Kind => PolicyKind;

    /// <summary>What the budget belongs to.</summary>
    public RateLimitDimension Dimension { get; init; } = RateLimitDimension.ClientAddress;

    /// <summary>How the window is shaped.</summary>
    public RateLimitAlgorithm Algorithm { get; init; } = RateLimitAlgorithm.SlidingWindow;

    /// <summary>Requests permitted inside <see cref="Window"/>, or concurrent calls for <see cref="RateLimitAlgorithm.Concurrency"/>.</summary>
    public int PermitLimit { get; init; } = 100;

    /// <summary>The window length. Ignored by <see cref="RateLimitAlgorithm.Concurrency"/>.</summary>
    public TimeSpan Window { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>Segments a sliding window is divided into. More segments, smoother edge, more state.</summary>
    public int SegmentsPerWindow { get; init; } = 6;

    /// <summary>
    /// Burst allowance above <see cref="PermitLimit"/> for <see cref="RateLimitAlgorithm.TokenBucket"/>.
    /// A mobile client that wakes up and fires six requests is not an attacker; a burst allowance is
    /// what stops the platform treating it as one.
    /// </summary>
    public int BurstCapacity { get; init; }

    /// <summary>Requests parked rather than refused when the budget is spent.</summary>
    public int QueueLimit { get; init; }

    /// <summary>
    /// Whether the limit is enforced. A policy that is off still appears on the dashboard with its
    /// numbers, which is what an operator needs before turning one on.
    /// </summary>
    public bool Enabled { get; init; } = true;

    public override IEnumerable<string> Validate()
    {
        if (PermitLimit < 1)
        {
            yield return "PermitLimit must be at least 1.";
        }

        if (Algorithm != RateLimitAlgorithm.Concurrency && Window <= TimeSpan.Zero)
        {
            yield return "Window must be positive for a time-based algorithm.";
        }

        if (Algorithm == RateLimitAlgorithm.SlidingWindow && SegmentsPerWindow < 1)
        {
            yield return "SegmentsPerWindow must be at least 1 for a sliding window.";
        }

        if (BurstCapacity < 0)
        {
            yield return "BurstCapacity cannot be negative.";
        }

        if (BurstCapacity > 0 && Algorithm != RateLimitAlgorithm.TokenBucket)
        {
            yield return "BurstCapacity is only honoured by the token-bucket algorithm.";
        }

        if (QueueLimit < 0)
        {
            yield return "QueueLimit cannot be negative.";
        }
    }
}
