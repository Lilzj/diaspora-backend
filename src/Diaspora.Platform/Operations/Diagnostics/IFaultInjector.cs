namespace Diaspora.Platform.Operations.Diagnostics;

/// <summary>What kind of fault can be injected.</summary>
public enum FaultKind
{
    /// <summary>Add latency to an operation.</summary>
    Latency = 0,

    /// <summary>Make a dependency fail.</summary>
    DependencyFailure = 1,

    /// <summary>Make the messaging transport unavailable.</summary>
    QueueOutage = 2,

    /// <summary>Make the cache unavailable.</summary>
    CacheOutage = 3,

    /// <summary>Make object storage unavailable.</summary>
    StorageOutage = 4
}

/// <summary>
/// The chaos-readiness seam.
/// <para>
/// <b>No chaos tooling ships, and that is the brief's instruction as well as the right call.</b> A
/// scheduler that injects faults on its own needs a blast-radius model, a safety interlock and an
/// abort path, none of which is worth building before anybody has run a single deliberate experiment.
/// What is worth building — and what cannot be added later without touching every call site — is the
/// point at which a fault could be injected.
/// </para>
/// <para>
/// <b>The shipped implementation always answers "no fault", and the injection points are on paths
/// that already cost a policy lookup</b>, so the cost of having them is one dictionary probe against a
/// dictionary that is empty. A deployment that wants to run an experiment registers an implementation;
/// nothing else changes.
/// </para>
/// <para>
/// An implementation that could be enabled in production without an explicit, separately-configured
/// opt-in would be a way to cause an outage through configuration. That constraint belongs to whoever
/// writes one, and it is written here so the next reader inherits it.
/// </para>
/// </summary>
public interface IFaultInjector
{
    /// <summary>Whether any fault is currently configured. False makes every other call a no-op.</summary>
    bool IsActive { get; }

    /// <summary>
    /// Applies whatever fault is configured for <paramref name="operation"/>.
    /// <para>
    /// Called at the start of an instrumented operation. Returns normally when no fault applies, delays
    /// for a latency fault, and throws for a failure fault — which is what makes an injected failure
    /// indistinguishable from a real one to everything downstream, including the circuit breaker.
    /// </para>
    /// </summary>
    /// <param name="kind">Which class of fault this point can simulate.</param>
    /// <param name="operation">What is being called — a dependency name, a queue name, a cache namespace.</param>
    ValueTask InjectAsync(
        FaultKind kind, string operation, CancellationToken cancellationToken = default);
}
