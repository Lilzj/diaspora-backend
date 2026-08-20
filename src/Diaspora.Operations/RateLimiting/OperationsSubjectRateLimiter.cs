using Diaspora.Platform.Operations.Policies;
using Diaspora.Platform.Operations.RateLimiting;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Operations.RateLimiting;

/// <summary>
/// Serves the kernel's <see cref="ISubjectRateLimiter"/> from the platform limiter.
/// <para>
/// <b>An adapter rather than a second implementation, and that is the point of B17-R4.</b> Identity's
/// sign-in, OTP and recovery paths call <c>ISubjectRateLimiter</c> with an explicit limit and window
/// baked into the call site; the transport limiter, the module limits and this one were three separate
/// pieces of state with three separate sets of numbers. Routing them all through
/// <see cref="IPlatformRateLimiter"/> means one implementation, one set of partitions, and one metric —
/// so an operator looking at rate-limit rejections sees every refusal on the platform rather than a
/// subset.
/// </para>
/// <para>
/// <b>The caller's limit and window still win.</b> A synthetic policy is built per call from the
/// arguments, which keeps Identity's behaviour byte-for-byte what it was: this phase does not get to
/// silently retune the sign-in limiter. Moving those numbers into the policy catalogue is Identity's
/// call to make, not the platform's to make on its behalf.
/// </para>
/// </summary>
public sealed class OperationsSubjectRateLimiter : ISubjectRateLimiter
{
    private readonly PlatformRateLimiter _limiter;
    private readonly SyntheticPolicyCatalog _catalog;

    public OperationsSubjectRateLimiter(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        _catalog = new SyntheticPolicyCatalog();
        _limiter = new PlatformRateLimiter(_catalog, clock);
    }

    public async ValueTask<RateLimitLease> AcquireAsync(
        string key,
        int permitLimit,
        TimeSpan window,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentOutOfRangeException.ThrowIfLessThan(permitLimit, 1);

        var policyName = _catalog.Ensure(key, permitLimit, window);

        var decision = await _limiter
            .AcquireAsync(policyName, new RateLimitRequest(Subject: key), cancellationToken)
            .ConfigureAwait(false);

        return decision.IsAllowed
            ? RateLimitLease.Allowed(decision.Remaining)
            : RateLimitLease.Denied(decision.RetryAfter);
    }

    public ValueTask ResetAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var policyName = _catalog.Find(key);

        return policyName is null
            ? ValueTask.CompletedTask
            : _limiter.ResetAsync(policyName, new RateLimitRequest(Subject: key), cancellationToken);
    }

    /// <summary>
    /// Holds one policy per caller-supplied key shape.
    /// <para>
    /// A private catalogue rather than the platform's: these policies are synthesised from a call site's
    /// arguments and have no version, no description and no business on the operational dashboard beside
    /// policies somebody declared. Keeping them here means the composed catalogue stays exactly the set
    /// a module or a deployment wrote.
    /// </para>
    /// <para>
    /// Keyed on the key's namespace — the part before the first colon — rather than on the whole key.
    /// One policy per purpose, partitioned per subject by the limiter, which is what a purpose-scoped
    /// key like <c>signin:+2348012345678</c> already means. Keying on the whole key would create one
    /// policy per phone number.
    /// </para>
    /// </summary>
    private sealed class SyntheticPolicyCatalog : IOperationalPolicyCatalog
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, RateLimitPolicy> _policies =
            new(StringComparer.Ordinal);

        public IReadOnlyList<OperationalPolicy> All => [.. _policies.Values];

        public string Ensure(string key, int permitLimit, TimeSpan window)
        {
            var purpose = Purpose(key);

            _policies.AddOrUpdate(
                purpose,
                static (name, state) => Build(name, state.PermitLimit, state.Window),
                static (name, existing, state) =>
                    existing.PermitLimit == state.PermitLimit && existing.Window == state.Window
                        ? existing
                        : Build(name, state.PermitLimit, state.Window),
                (PermitLimit: permitLimit, Window: window));

            return purpose;
        }

        public string? Find(string key)
        {
            var purpose = Purpose(key);

            return _policies.ContainsKey(purpose) ? purpose : null;
        }

        public TPolicy Get<TPolicy>(string name, TPolicy fallback) where TPolicy : OperationalPolicy =>
            Find<TPolicy>(name) ?? fallback;

        public TPolicy? Find<TPolicy>(string name) where TPolicy : OperationalPolicy =>
            _policies.GetValueOrDefault(name) as TPolicy;

        private static RateLimitPolicy Build(string name, int permitLimit, TimeSpan window) =>
            new(name)
            {
                Description = "Synthesised from an ISubjectRateLimiter call site.",
                Dimension = RateLimitDimension.Subject,
                Algorithm = RateLimitAlgorithm.SlidingWindow,
                PermitLimit = permitLimit,
                Window = window
            };

        private static string Purpose(string key)
        {
            var separator = key.IndexOf(':', StringComparison.Ordinal);

            return separator <= 0 ? key : key[..separator];
        }
    }
}
