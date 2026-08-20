using System.Collections.Concurrent;
using Diaspora.Operations.Telemetry;
using Diaspora.Platform.Operations.Policies;
using Diaspora.Platform.Operations.RateLimiting;
using Diaspora.Platform.Operations.Telemetry;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Operations.RateLimiting;

/// <summary>
/// The in-process implementation of every rate limit on the platform.
/// <para>
/// Four algorithms behind one contract. Which one applies, what it partitions on and what the numbers
/// are all come from the resolved <see cref="RateLimitPolicy"/>, so a deployment changes a limit by
/// shipping a policy version rather than by touching this file.
/// </para>
/// <para>
/// <b>Buckets are evicted lazily, on read.</b> A sweep timer over a dictionary that is mostly cold is
/// a background thread doing nothing useful most of the time; checking the bucket you are already
/// touching costs nothing. The consequence is that a bucket for a key nobody has used since is never
/// reclaimed — which is why <see cref="Compact"/> exists and why the maintenance worker calls it.
/// </para>
/// </summary>
public sealed class PlatformRateLimiter : IPlatformRateLimiter
{
    private readonly ConcurrentDictionary<string, Bucket> _buckets = new(StringComparer.Ordinal);
    private readonly IOperationalPolicyCatalog _policies;
    private readonly IClock _clock;

    public PlatformRateLimiter(IOperationalPolicyCatalog policies, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(clock);

        _policies = policies;
        _clock = clock;
    }

    public ValueTask<RateLimitDecision> AcquireAsync(
        string policyName,
        RateLimitRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        cancellationToken.ThrowIfCancellationRequested();

        var policy = _policies.Find<RateLimitPolicy>(policyName);

        if (policy is null || !policy.Enabled)
        {
            return ValueTask.FromResult(RateLimitDecision.NoLimit);
        }

        var key = KeyFor(policy, request);
        var bucket = _buckets.GetOrAdd(key, static _ => new Bucket());
        var decision = bucket.Acquire(policy, _clock.UtcNow);

        var tags = new System.Diagnostics.TagList
        {
            { PlatformTelemetryNames.Attributes.Policy, policy.Name },
            { PlatformTelemetryNames.Attributes.LimitDimension, policy.Dimension.ToString() },
            { PlatformTelemetryNames.Attributes.Outcome, decision.IsAllowed ? "allowed" : "rejected" },
            { PlatformTelemetryNames.Attributes.Module, request.Module ?? "platform" }
        };

        OperationsTelemetry.RateLimitDecisions.Add(1, tags);

        if (!decision.IsAllowed)
        {
            OperationsTelemetry.RateLimitRejections.Add(1, tags);
        }

        return ValueTask.FromResult(decision);
    }

    public ValueTask ReleaseAsync(
        string policyName,
        RateLimitRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        cancellationToken.ThrowIfCancellationRequested();

        var policy = _policies.Find<RateLimitPolicy>(policyName);

        if (policy is not { Algorithm: RateLimitAlgorithm.Concurrency })
        {
            return ValueTask.CompletedTask;
        }

        if (_buckets.TryGetValue(KeyFor(policy, request), out var bucket))
        {
            bucket.Release();
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask ResetAsync(
        string policyName,
        RateLimitRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        cancellationToken.ThrowIfCancellationRequested();

        var policy = _policies.Find<RateLimitPolicy>(policyName);

        if (policy is not null)
        {
            _buckets.TryRemove(KeyFor(policy, request), out _);
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Discards buckets that have been idle longer than <paramref name="idleFor"/>.
    /// <para>
    /// Called by the maintenance worker. A limiter partitioned on client address accumulates one
    /// bucket per address that has ever called, and the addresses that matter — the ones from an
    /// attack — are exactly the ones that stop calling and never come back.
    /// </para>
    /// </summary>
    /// <returns>How many buckets were discarded.</returns>
    public int Compact(TimeSpan idleFor)
    {
        var cutoff = _clock.UtcNow - idleFor;
        var removed = 0;

        foreach (var entry in _buckets)
        {
            if (entry.Value.IsIdleSince(cutoff) &&
                _buckets.TryRemove(new KeyValuePair<string, Bucket>(entry.Key, entry.Value)))
            {
                removed++;
            }
        }

        return removed;
    }

    /// <summary>Live bucket count, for the operational dashboard.</summary>
    public int BucketCount => _buckets.Count;

    private static string KeyFor(RateLimitPolicy policy, RateLimitRequest request) =>
        $"{policy.Name}|{policy.Dimension}|{request.Value(policy.Dimension)}";

    /// <summary>
    /// One partition's state, for whichever algorithm the policy names.
    /// <para>
    /// One class rather than four because the state is nearly identical and the branch is one switch;
    /// four classes behind an interface would add a virtual call and an allocation per partition to
    /// avoid a switch statement that fits on a screen.
    /// </para>
    /// </summary>
    private sealed class Bucket
    {
        private readonly Lock _gate = new();

        private long[] _segments = [];
        private int _segmentHead;
        private DateTimeOffset _segmentStartedAt;
        private DateTimeOffset _windowStartedAt;
        private long _windowCount;
        private double _tokens = -1;
        private DateTimeOffset _refilledAt;
        private int _inFlight;
        private DateTimeOffset _touchedAt;

        public bool IsIdleSince(DateTimeOffset cutoff)
        {
            lock (_gate)
            {
                // A partition with calls in flight is never idle, however long ago it was touched:
                // discarding it would lose the count and let the next caller exceed the limit.
                return _inFlight == 0 && _touchedAt < cutoff;
            }
        }

        public void Release()
        {
            lock (_gate)
            {
                if (_inFlight > 0)
                {
                    _inFlight--;
                }
            }
        }

        public RateLimitDecision Acquire(RateLimitPolicy policy, DateTimeOffset now)
        {
            lock (_gate)
            {
                _touchedAt = now;

                return policy.Algorithm switch
                {
                    RateLimitAlgorithm.FixedWindow => FixedWindow(policy, now),
                    RateLimitAlgorithm.SlidingWindow => SlidingWindow(policy, now),
                    RateLimitAlgorithm.TokenBucket => TokenBucket(policy, now),
                    RateLimitAlgorithm.Concurrency => Concurrency(policy),
                    _ => RateLimitDecision.NoLimit
                };
            }
        }

        private RateLimitDecision FixedWindow(RateLimitPolicy policy, DateTimeOffset now)
        {
            if (now - _windowStartedAt >= policy.Window)
            {
                _windowStartedAt = now;
                _windowCount = 0;
            }

            if (_windowCount >= policy.PermitLimit)
            {
                return RateLimitDecision.Deny(
                    _windowStartedAt + policy.Window - now, policy.Name, policy.Dimension);
            }

            _windowCount++;

            return RateLimitDecision.Allow(
                (int)(policy.PermitLimit - _windowCount), policy.Name, policy.Dimension);
        }

        /// <summary>
        /// A segmented window.
        /// <para>
        /// The reason this is the platform's default: a fixed window permits a double-rate burst
        /// across its boundary — a client spending its whole budget in the last second of one window
        /// and again in the first second of the next has made twice the permitted requests in two
        /// seconds, entirely within the rules.
        /// </para>
        /// </summary>
        private RateLimitDecision SlidingWindow(RateLimitPolicy policy, DateTimeOffset now)
        {
            var segments = Math.Max(1, policy.SegmentsPerWindow);

            if (_segments.Length != segments)
            {
                _segments = new long[segments];
                _segmentHead = 0;
                _segmentStartedAt = now;
            }

            var segmentDuration = policy.Window / segments;
            var elapsed = now - _segmentStartedAt;

            if (segmentDuration > TimeSpan.Zero && elapsed >= segmentDuration)
            {
                var advance = (long)(elapsed.Ticks / segmentDuration.Ticks);

                if (advance >= segments)
                {
                    Array.Clear(_segments);
                    _segmentHead = 0;
                }
                else
                {
                    for (var step = 0; step < advance; step++)
                    {
                        _segmentHead = (_segmentHead + 1) % segments;
                        _segments[_segmentHead] = 0;
                    }
                }

                _segmentStartedAt = now;
            }

            long total = 0;

            foreach (var segment in _segments)
            {
                total += segment;
            }

            if (total >= policy.PermitLimit)
            {
                // The oldest segment is the next to age out, so that is when capacity returns.
                return RateLimitDecision.Deny(segmentDuration, policy.Name, policy.Dimension);
            }

            _segments[_segmentHead]++;

            return RateLimitDecision.Allow(
                (int)(policy.PermitLimit - total - 1), policy.Name, policy.Dimension);
        }

        /// <summary>
        /// Continuous refill with a burst allowance.
        /// <para>
        /// The right shape for client traffic. A mobile app returning to the foreground fires a
        /// screen's worth of requests at once and is not an attacker; a window-based limit either
        /// refuses it or is set so high that it stops being a limit.
        /// </para>
        /// </summary>
        private RateLimitDecision TokenBucket(RateLimitPolicy policy, DateTimeOffset now)
        {
            var capacity = policy.PermitLimit + policy.BurstCapacity;

            if (_tokens < 0)
            {
                _tokens = capacity;
                _refilledAt = now;
            }

            var perSecond = policy.PermitLimit / Math.Max(policy.Window.TotalSeconds, 0.001);
            var elapsed = (now - _refilledAt).TotalSeconds;

            if (elapsed > 0)
            {
                _tokens = Math.Min(capacity, _tokens + (elapsed * perSecond));
                _refilledAt = now;
            }

            if (_tokens < 1)
            {
                var seconds = (1 - _tokens) / Math.Max(perSecond, 0.001);
                return RateLimitDecision.Deny(
                    TimeSpan.FromSeconds(seconds), policy.Name, policy.Dimension);
            }

            _tokens -= 1;

            return RateLimitDecision.Allow((int)_tokens, policy.Name, policy.Dimension);
        }

        private RateLimitDecision Concurrency(RateLimitPolicy policy)
        {
            if (_inFlight >= policy.PermitLimit)
            {
                // No meaningful retry-after: a concurrency limit frees when a call finishes, and
                // guessing when that will be would produce a number the client would trust.
                return RateLimitDecision.Deny(TimeSpan.Zero, policy.Name, policy.Dimension);
            }

            _inFlight++;

            return RateLimitDecision.Allow(policy.PermitLimit - _inFlight, policy.Name, policy.Dimension);
        }
    }
}
