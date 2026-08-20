namespace Diaspora.Platform.Operations.Resilience;

/// <summary>
/// Runs an operation under the platform's resilience policies.
/// <para>
/// <b>This is the only place retry, circuit breaking, bulkheading and per-call timeouts are
/// implemented on the platform.</b> That is an architecture rule (B17-R4) rather than a preference:
/// a retry loop written inside an adapter is invisible to the dashboard, uncoordinated with the
/// breaker that is supposed to stop it, and impossible to tune without a deployment. The suite fails
/// a build that adds one.
/// </para>
/// <para>
/// The composition order is fixed and it is the only order that works:
/// <list type="number">
/// <item>Bulkhead — refuse before spending a thread on something the platform has no capacity for.</item>
/// <item>Circuit breaker — refuse before calling a dependency believed to be down.</item>
/// <item>Retry — the loop, so each attempt is separately seen by the breaker.</item>
/// <item>Timeout — per attempt, so a hung call cannot consume the whole retry budget.</item>
/// </list>
/// Putting retry outside the breaker rather than inside it is the mistake worth naming: a breaker
/// that only sees the final failure of a three-attempt sequence needs three times the traffic to
/// open, which is three times the load on a dependency that is already failing.
/// </para>
/// </summary>
public interface IResilienceExecutor
{
    /// <summary>
    /// Runs <paramref name="operation"/> under the policies registered for
    /// <paramref name="dependency"/>.
    /// </summary>
    /// <param name="dependency">
    /// What is being called — <c>financial.payment-provider</c>, <c>media.provider</c>. Resolves the
    /// retry policy, the breaker policy and the bulkhead, and labels every metric emitted.
    /// </param>
    /// <param name="operation">The call. Must be safe to run more than once.</param>
    Task<TResult> ExecuteAsync<TResult>(
        string dependency,
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);

    /// <summary>The void form.</summary>
    Task ExecuteAsync(
        string dependency,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);
}

/// <summary>Reads and controls circuit breakers. For the dashboard and for the operator.</summary>
public interface ICircuitBreakerRegistry
{
    /// <summary>Every breaker that has been exercised, and its state.</summary>
    IReadOnlyList<CircuitBreakerState> All { get; }

    CircuitBreakerState? Find(string dependency);

    /// <summary>
    /// Holds a breaker open until <see cref="Reset"/>.
    /// <para>
    /// The lever for a planned dependency outage: isolating a payment provider before its maintenance
    /// window converts a wave of timeouts into immediate, correctly-typed refusals. It is not
    /// automatic and never expires, because a manual isolation that silently healed would be worse
    /// than none.
    /// </para>
    /// </summary>
    void Isolate(string dependency);

    /// <summary>Returns an isolated or open breaker to closed.</summary>
    void Reset(string dependency);
}

/// <summary>Reads bulkhead occupancy. For the dashboard.</summary>
public interface IBulkheadRegistry
{
    IReadOnlyList<BulkheadState> All { get; }

    BulkheadState? Find(string name);
}
