using Diaspora.Platform.Operations.Policies;
using Diaspora.Platform.Operations.Resilience;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Operations.Resilience;

/// <summary>
/// One circuit breaker, for one dependency.
/// <para>
/// A rolling window of outcome counts, bucketed. Bucketing rather than a list of timestamped
/// outcomes because a busy dependency would otherwise accumulate one entry per call for the whole
/// sampling window — thousands of allocations per breaker per window, to compute one ratio.
/// </para>
/// <para>
/// <b>The window advances lazily.</b> Nothing ticks; buckets are aged on the next call. A breaker
/// with no traffic holds whatever it last measured and nobody reads it, which is correct: a
/// dependency nothing is calling has no failure ratio.
/// </para>
/// </summary>
public sealed class CircuitBreaker
{
    private const int BucketCount = 10;

    private readonly Lock _gate = new();
    private readonly long[] _successes = new long[BucketCount];
    private readonly long[] _failures = new long[BucketCount];
    private readonly IClock _clock;

    private CircuitBreakerPolicy _policy;
    private int _currentBucket;
    private DateTimeOffset _bucketStartedAt;
    private CircuitState _state = CircuitState.Closed;
    private DateTimeOffset? _openedAt;
    private DateTimeOffset _closesAt;
    private int _halfOpenInFlight;
    private long _totalTrips;

    public CircuitBreaker(string dependency, CircuitBreakerPolicy policy, IClock clock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dependency);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(clock);

        Dependency = dependency;
        _policy = policy;
        _clock = clock;
        _bucketStartedAt = clock.UtcNow;
    }

    public string Dependency { get; }

    /// <summary>
    /// Replaces the policy without discarding the window.
    /// <para>
    /// Called when runtime configuration changes a breaker's numbers. Keeping the window is the point:
    /// an operator loosening a breaker during an incident wants it to reassess with the traffic it has
    /// already seen, not to start from zero and need a full window before it can act again.
    /// </para>
    /// </summary>
    public void Retune(CircuitBreakerPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        lock (_gate)
        {
            _policy = policy;
        }
    }

    /// <summary>
    /// Whether a call may proceed, and closes over the bookkeeping if it may.
    /// </summary>
    /// <param name="closesAt">When the circuit will next admit a call. Only meaningful on a refusal.</param>
    /// <param name="trialCall">
    /// Whether this call consumed a half-open slot. Reported rather than inferred from a later
    /// snapshot: between <c>TryEnter</c> and the call finishing, another thread can move the state,
    /// and a caller that re-read it would release a slot it never held.
    /// </param>
    public bool TryEnter(out DateTimeOffset closesAt, out bool trialCall)
    {
        lock (_gate)
        {
            var now = _clock.UtcNow;
            Age(now);
            trialCall = false;

            switch (_state)
            {
                case CircuitState.Isolated:
                    closesAt = DateTimeOffset.MaxValue;
                    return false;

                case CircuitState.Open when now < _closesAt:
                    closesAt = _closesAt;
                    return false;

                case CircuitState.Open:
                    // The break has elapsed. Admit one trial call and hold everything else.
                    _state = CircuitState.HalfOpen;
                    _halfOpenInFlight = 1;
                    closesAt = _closesAt;
                    trialCall = true;
                    return true;

                case CircuitState.HalfOpen when _halfOpenInFlight >= _policy.HalfOpenPermittedCalls:
                    closesAt = _closesAt;
                    return false;

                case CircuitState.HalfOpen:
                    _halfOpenInFlight++;
                    closesAt = _closesAt;
                    trialCall = true;
                    return true;

                default:
                    closesAt = default;
                    return true;
            }
        }
    }

    /// <summary>Records a success. Closes a half-open circuit.</summary>
    public void OnSuccess()
    {
        lock (_gate)
        {
            var now = _clock.UtcNow;
            Age(now);

            _successes[_currentBucket]++;

            if (_state != CircuitState.HalfOpen)
            {
                return;
            }

            // The trial call worked. Close, and clear the window: keeping the failures that opened
            // the circuit would reopen it on the very next failure, no matter how much traffic had
            // succeeded in between.
            _state = CircuitState.Closed;
            _halfOpenInFlight = 0;
            _openedAt = null;
            Array.Clear(_successes);
            Array.Clear(_failures);
        }
    }

    /// <summary>Records a failure, and opens the circuit if the window says to.</summary>
    /// <returns>True when this failure opened the circuit.</returns>
    public bool OnFailure()
    {
        lock (_gate)
        {
            var now = _clock.UtcNow;
            Age(now);

            _failures[_currentBucket]++;

            if (_state == CircuitState.HalfOpen)
            {
                // The trial call failed. Straight back to open for a full break duration — a
                // dependency that just failed its probe should not be probed again immediately.
                Open(now);
                return true;
            }

            if (_state != CircuitState.Closed)
            {
                return false;
            }

            var failures = Sum(_failures);
            var throughput = failures + Sum(_successes);

            if (throughput < _policy.MinimumThroughput)
            {
                return false;
            }

            if ((double)failures / throughput < _policy.FailureRatio)
            {
                return false;
            }

            Open(now);
            return true;
        }
    }

    /// <summary>Releases a half-open slot when a trial call finishes without changing state.</summary>
    public void OnHalfOpenReleased()
    {
        lock (_gate)
        {
            if (_state == CircuitState.HalfOpen && _halfOpenInFlight > 0)
            {
                _halfOpenInFlight--;
            }
        }
    }

    public void Isolate()
    {
        lock (_gate)
        {
            _state = CircuitState.Isolated;
            _openedAt = _clock.UtcNow;
            _closesAt = DateTimeOffset.MaxValue;
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _state = CircuitState.Closed;
            _openedAt = null;
            _halfOpenInFlight = 0;
            Array.Clear(_successes);
            Array.Clear(_failures);
        }
    }

    public CircuitBreakerState Snapshot()
    {
        lock (_gate)
        {
            var now = _clock.UtcNow;
            Age(now);

            var failures = Sum(_failures);
            var throughput = failures + Sum(_successes);

            return new CircuitBreakerState(
                Dependency,
                _state,
                throughput == 0 ? 0 : (double)failures / throughput,

                // Narrowed deliberately. This is a count inside one sampling window — thirty seconds
                // by default — and a deployment serving two billion calls to one dependency in that
                // window has a problem no breaker is going to help with.
                (int)Math.Min(throughput, int.MaxValue),
                _openedAt,
                _state == CircuitState.Open ? _closesAt : null,
                _totalTrips);
        }
    }

    private void Open(DateTimeOffset now)
    {
        _state = CircuitState.Open;
        _openedAt = now;
        _closesAt = now + _policy.BreakDuration;
        _halfOpenInFlight = 0;
        _totalTrips++;
    }

    /// <summary>
    /// Advances the bucket ring to <paramref name="now"/>, clearing whatever has aged out.
    /// <para>
    /// A gap longer than the whole sampling window clears everything, which is the correct answer:
    /// counts from before a quiet period say nothing about a dependency's health now.
    /// </para>
    /// </summary>
    private void Age(DateTimeOffset now)
    {
        var bucketDuration = _policy.SamplingDuration / BucketCount;

        if (bucketDuration <= TimeSpan.Zero)
        {
            return;
        }

        var elapsed = now - _bucketStartedAt;

        if (elapsed < bucketDuration)
        {
            return;
        }

        var advance = (long)(elapsed.Ticks / bucketDuration.Ticks);

        if (advance >= BucketCount)
        {
            Array.Clear(_successes);
            Array.Clear(_failures);
            _currentBucket = 0;
        }
        else
        {
            for (var step = 0; step < advance; step++)
            {
                _currentBucket = (_currentBucket + 1) % BucketCount;
                _successes[_currentBucket] = 0;
                _failures[_currentBucket] = 0;
            }
        }

        _bucketStartedAt = now;
    }

    private static long Sum(long[] buckets)
    {
        long total = 0;

        foreach (var bucket in buckets)
        {
            total += bucket;
        }

        return total;
    }
}
