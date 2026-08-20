using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using Diaspora.Operations.Telemetry;
using Diaspora.Platform.Operations.Policies;
using Diaspora.Platform.Operations.Resilience;
using Diaspora.Platform.Operations.Telemetry;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace Diaspora.Operations.Resilience;

/// <summary>
/// The platform's single implementation of retry, circuit breaking, bulkheading and per-call timeout.
/// <para>
/// Composed in one fixed order — bulkhead, breaker, retry, timeout — for the reasons set out on
/// <see cref="IResilienceExecutor"/>. Everything it needs comes from the operational policy
/// catalogue, so tuning any of it is a policy version rather than a code change.
/// </para>
/// <para>
/// <b>No third-party resilience library.</b> Not on principle — the good ones are good — but because
/// the platform's requirement is that policies are catalogue-driven, versioned, visible on a
/// dashboard and adjustable at runtime, and every library models a policy as a compiled pipeline
/// built at registration. Wrapping one would mean either rebuilding pipelines on every policy change
/// or accepting that a policy change needs a deployment, and the second is the thing this phase
/// exists to remove. What is left is about two hundred lines, all of it visible here.
/// </para>
/// </summary>
public sealed class ResilienceExecutor : IResilienceExecutor, ICircuitBreakerRegistry, IBulkheadRegistry, IDisposable
{
    private static readonly RetryPolicy DefaultRetry = new(OperationalPolicyNames.Retry.Default);
    private static readonly CircuitBreakerPolicy DefaultBreaker = new(OperationalPolicyNames.CircuitBreaker.ExternalApi);

    private readonly ConcurrentDictionary<string, CircuitBreaker> _breakers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Bulkhead> _bulkheads = new(StringComparer.Ordinal);
    private readonly IReadOnlyDictionary<string, IFailureClassifier> _classifiers;
    private readonly IFailureClassifier _defaultClassifier;
    private readonly IOperationalPolicyCatalog _policies;
    private readonly IClock _clock;
    private readonly ILogger<ResilienceExecutor> _logger;

    public ResilienceExecutor(
        IOperationalPolicyCatalog policies,
        IEnumerable<IFailureClassifier> classifiers,
        IClock clock,
        ILogger<ResilienceExecutor> logger)
    {
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(classifiers);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _policies = policies;
        _clock = clock;
        _logger = logger;

        var registered = classifiers as IReadOnlyList<IFailureClassifier> ?? [.. classifiers];

        _classifiers = registered
            .Where(classifier => classifier.Dependency is not null)
            .GroupBy(classifier => classifier.Dependency!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);

        _defaultClassifier = registered.LastOrDefault(classifier => classifier.Dependency is null)
            ?? new DefaultFailureClassifier();
    }

    public async Task<TResult> ExecuteAsync<TResult>(
        string dependency,
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dependency);
        ArgumentNullException.ThrowIfNull(operation);

        var retry = RetryFor(dependency);
        var breakerPolicy = BreakerPolicyFor(dependency);
        var breaker = BreakerFor(dependency, breakerPolicy);
        var classifier = _classifiers.GetValueOrDefault(dependency, _defaultClassifier);

        using var activity = OperationsTelemetry.Source.StartActivity(
            $"resilient {dependency}", ActivityKind.Internal);

        activity?.SetTag(PlatformTelemetryNames.Attributes.Dependency, dependency);
        activity?.SetTag(PlatformTelemetryNames.Attributes.Policy, retry.ToString());

        // ─── 1. Bulkhead ───
        var bulkhead = BulkheadFor(dependency);
        using var slot = await bulkhead.EnterAsync(cancellationToken).ConfigureAwait(false);

        var deadline = retry.OverallTimeout is { } overall ? _clock.UtcNow + overall : (DateTimeOffset?)null;

        Exception? last = null;

        for (var attempt = 0; attempt <= retry.MaxAttempts; attempt++)
        {
            // ─── 2. Circuit breaker ───
            if (!breaker.TryEnter(out var closesAt, out var trialCall))
            {
                activity?.SetStatus(ActivityStatusCode.Error, "circuit open");
                throw new CircuitOpenException(dependency, closesAt);
            }

            try
            {
                // ─── 4. Per-attempt timeout ───
                var result = await RunAttemptAsync(operation, breakerPolicy, cancellationToken)
                    .ConfigureAwait(false);

                breaker.OnSuccess();
                activity?.SetStatus(ActivityStatusCode.Ok);

                return result;
            }
            catch (Exception exception)
            {
                var classification = classifier.Classify(exception);

                if (classification == FailureClassification.Cancelled)
                {
                    // Never counted. A member closing an app must not contribute to a breaker's
                    // failure ratio, and a cancelled call is not evidence about a dependency.
                    if (trialCall)
                    {
                        breaker.OnHalfOpenReleased();
                    }

                    throw;
                }

                var opened = breaker.OnFailure();

                if (opened)
                {
                    OperationsTelemetry.CircuitTransitions.Add(1,
                        new KeyValuePair<string, object?>(PlatformTelemetryNames.Attributes.Dependency, dependency),
                        new KeyValuePair<string, object?>(PlatformTelemetryNames.Attributes.Outcome, "open"));

                    _logger.LogError(
                        exception,
                        "Circuit for {Dependency} opened after crossing a {Ratio:P0} failure ratio. " +
                        "Calls will be refused for {Break}.",
                        dependency, breakerPolicy.FailureRatio, breakerPolicy.BreakDuration);
                }

                last = exception;

                if (classification is FailureClassification.Permanent or FailureClassification.Poison)
                {
                    activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
                    throw;
                }

                if (attempt >= retry.MaxAttempts)
                {
                    break;
                }

                var delay = DelayFor(retry, attempt);

                if (deadline is { } limit && _clock.UtcNow + delay > limit)
                {
                    _logger.LogWarning(
                        "Retry of {Dependency} abandoned: the next attempt would exceed the policy's " +
                        "overall timeout of {Timeout}.",
                        dependency, retry.OverallTimeout);

                    break;
                }

                OperationsTelemetry.Retries.Add(1,
                    new KeyValuePair<string, object?>(PlatformTelemetryNames.Attributes.Dependency, dependency),
                    new KeyValuePair<string, object?>(PlatformTelemetryNames.Attributes.Policy, retry.Name),
                    new KeyValuePair<string, object?>(
                        PlatformTelemetryNames.Attributes.Reason, classification.ToString()));

                _logger.LogWarning(
                    "Attempt {Attempt} of {Max} against {Dependency} failed ({Reason}). Retrying in {Delay}ms.",
                    attempt + 1,
                    retry.MaxAttempts + 1,
                    dependency,
                    classification,
                    delay.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture));

                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }

        OperationsTelemetry.RetryExhausted.Add(1,
            new KeyValuePair<string, object?>(PlatformTelemetryNames.Attributes.Dependency, dependency));

        activity?.SetStatus(ActivityStatusCode.Error, last?.Message ?? "retry exhausted");

        throw last ?? new InvalidOperationException(
            $"The operation against '{dependency}' failed and no exception was captured.");
    }

    public async Task ExecuteAsync(
        string dependency,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await ExecuteAsync<object?>(
            dependency,
            async token =>
            {
                await operation(token).ConfigureAwait(false);
                return null;
            },
            cancellationToken).ConfigureAwait(false);
    }

    // ─── Registries ──────────────────────────────────────────────────────────────────────

    public IReadOnlyList<CircuitBreakerState> All =>
        [.. _breakers.Values.Select(breaker => breaker.Snapshot())
            .OrderBy(state => state.Dependency, StringComparer.Ordinal)];

    public CircuitBreakerState? Find(string dependency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dependency);

        return _breakers.TryGetValue(dependency, out var breaker) ? breaker.Snapshot() : null;
    }

    public void Isolate(string dependency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dependency);

        BreakerFor(dependency, BreakerPolicyFor(dependency)).Isolate();

        _logger.LogWarning(
            "Circuit for {Dependency} was isolated by an operator. It will refuse every call until it " +
            "is reset — nothing will close it automatically.",
            dependency);
    }

    public void Reset(string dependency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dependency);

        if (_breakers.TryGetValue(dependency, out var breaker))
        {
            breaker.Reset();
            _logger.LogInformation("Circuit for {Dependency} was reset by an operator.", dependency);
        }
    }

    IReadOnlyList<BulkheadState> IBulkheadRegistry.All =>
        [.. _bulkheads.Values.Select(bulkhead => bulkhead.Snapshot())
            .OrderBy(state => state.Name, StringComparer.Ordinal)];

    BulkheadState? IBulkheadRegistry.Find(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return _bulkheads.TryGetValue(name, out var bulkhead) ? bulkhead.Snapshot() : null;
    }

    public void Dispose()
    {
        foreach (var bulkhead in _bulkheads.Values)
        {
            bulkhead.Dispose();
        }

        _bulkheads.Clear();
    }

    // ─── Internals ───────────────────────────────────────────────────────────────────────

    private static async Task<TResult> RunAttemptAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CircuitBreakerPolicy policy,
        CancellationToken cancellationToken)
    {
        if (policy.CallTimeout is not { } timeout)
        {
            return await operation(cancellationToken).ConfigureAwait(false);
        }

        using var attemptToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        attemptToken.CancelAfter(timeout);

        try
        {
            return await operation(attemptToken.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The attempt timed out rather than the caller giving up. Re-thrown as a TimeoutException
            // so the classifier sees a transient failure and the breaker counts it — a breaker that
            // treats a timeout as a cancellation never opens against a dependency that has become
            // slow, which is the failure mode that hurts most.
            throw new TimeoutException(
                $"The attempt exceeded the policy's call timeout of {timeout}.");
        }
    }

    /// <summary>
    /// Exponential backoff with jitter, clamped to the policy's ceiling.
    /// <para>
    /// The jitter is full-width around the computed delay rather than one-sided, which spreads a herd
    /// symmetrically. <see cref="Random.Shared"/> rather than a seeded instance: this is a scheduling
    /// nudge, not a security decision, and a cryptographic source would cost more than the delay it
    /// perturbs.
    /// </para>
    /// </summary>
    private static TimeSpan DelayFor(RetryPolicy policy, int attempt)
    {
        var growth = Math.Pow(policy.BackoffMultiplier, attempt);
        var delay = policy.InitialDelay.TotalMilliseconds * growth;

        delay = Math.Min(delay, policy.MaxDelay.TotalMilliseconds);

        if (policy.JitterRatio > 0)
        {
            var spread = delay * policy.JitterRatio;
            delay += (Random.Shared.NextDouble() * 2 - 1) * spread;
        }

        return TimeSpan.FromMilliseconds(Math.Max(0, delay));
    }

    /// <summary>
    /// The retry policy for a dependency: one named after it, else the catalogue's platform default,
    /// else the compiled fallback.
    /// <para>
    /// The two-step resolution is what makes a dependency-specific policy optional. Falling straight
    /// to the compiled fallback would ignore a deployment that had retuned <c>platform.default</c>,
    /// which is exactly the change an operator makes first and the one they would least expect to be
    /// ignored.
    /// </para>
    /// </summary>
    private RetryPolicy RetryFor(string dependency) =>
        _policies.Find<RetryPolicy>(dependency)
            ?? _policies.Find<RetryPolicy>(OperationalPolicyNames.Retry.Default)
            ?? DefaultRetry;

    private CircuitBreakerPolicy BreakerPolicyFor(string dependency) =>
        _policies.Find<CircuitBreakerPolicy>(dependency)
            ?? _policies.Find<CircuitBreakerPolicy>(OperationalPolicyNames.CircuitBreaker.ExternalApi)
            ?? DefaultBreaker;

    private CircuitBreaker BreakerFor(string dependency, CircuitBreakerPolicy policy)
    {
        var breaker = _breakers.GetOrAdd(
            dependency,
            static (key, state) => new CircuitBreaker(key, state.Policy, state.Clock),
            (Policy: policy, Clock: _clock));

        // Retuning on every resolve is what makes a runtime policy change take effect without a
        // restart. It is a field assignment under the breaker's own lock, and the window it has
        // already accumulated is deliberately kept.
        breaker.Retune(policy);

        return breaker;
    }

    /// <summary>
    /// The bulkhead for a dependency, sized from its rate-limit policy where one exists.
    /// <para>
    /// Sized from the rate-limit policy rather than from a separate one, because a bulkhead and a
    /// concurrency limit are the same number wearing two names, and giving them two policies is how
    /// they end up disagreeing. A dependency with no concurrency policy gets a generous default: the
    /// bulkhead is a backstop against a slow dependency, not a throttle.
    /// </para>
    /// </summary>
    private Bulkhead BulkheadFor(string dependency) =>
        _bulkheads.GetOrAdd(dependency, key =>
        {
            var limit = _policies.Find<RateLimitPolicy>(key);

            var capacity = limit is { Algorithm: RateLimitAlgorithm.Concurrency }
                ? limit.PermitLimit
                : DefaultBulkheadCapacity;

            var queue = limit?.QueueLimit ?? DefaultBulkheadQueue;

            return new Bulkhead(key, capacity, queue);
        });

    private const int DefaultBulkheadCapacity = 64;
    private const int DefaultBulkheadQueue = 128;
}
