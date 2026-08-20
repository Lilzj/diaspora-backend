namespace Diaspora.Platform.Operations.Resilience;

/// <summary>A circuit breaker's state.</summary>
public enum CircuitState
{
    /// <summary>Calls pass through. The normal state.</summary>
    Closed = 0,

    /// <summary>Calls are refused immediately. The dependency is believed to be down.</summary>
    Open = 1,

    /// <summary>A trial call is allowed through to see whether the dependency has recovered.</summary>
    HalfOpen = 2,

    /// <summary>Held open by an operator. Never closes on its own.</summary>
    Isolated = 3
}

/// <summary>How a failure should be treated.</summary>
public enum FailureClassification
{
    /// <summary>Worth retrying: a timeout, a reset connection, a 503.</summary>
    Transient = 0,

    /// <summary>Not worth retrying: a validation error, a 400, a 404. Retrying produces the same answer slower.</summary>
    Permanent = 1,

    /// <summary>
    /// The caller gave up. Never counted as a failure anywhere — a member closing an app must not
    /// contribute to a circuit breaker's failure ratio.
    /// </summary>
    Cancelled = 2,

    /// <summary>
    /// Repeatedly permanent for the same input. Removed from the retry cycle entirely and sent to a
    /// dead letter, because retrying it forever is how one bad message consumes a whole consumer.
    /// </summary>
    Poison = 3
}

/// <summary>
/// Decides whether a failure is worth retrying.
/// <para>
/// Registered per dependency where the default is wrong. The default classifier knows the shapes
/// the BCL raises — timeouts, socket errors, cancellation — and treats everything else as permanent,
/// which is the conservative answer: retrying a permanent failure wastes time and, on a
/// non-idempotent operation, can do real damage.
/// </para>
/// </summary>
public interface IFailureClassifier
{
    /// <summary>The dependency this classifier serves, or null for the platform default.</summary>
    string? Dependency { get; }

    FailureClassification Classify(Exception exception);
}

/// <summary>What one circuit breaker looks like right now.</summary>
/// <param name="Dependency">What it protects.</param>
/// <param name="State">Where it is.</param>
/// <param name="FailureRatio">Failures over calls in the current sampling window.</param>
/// <param name="Throughput">Calls in the current sampling window.</param>
/// <param name="OpenedAt">When it last opened. Null if it never has.</param>
/// <param name="ClosesAt">When it will admit a trial call. Null unless open.</param>
/// <param name="TotalTrips">How many times it has opened since the process started.</param>
public sealed record CircuitBreakerState(
    string Dependency,
    CircuitState State,
    double FailureRatio,
    int Throughput,
    DateTimeOffset? OpenedAt,
    DateTimeOffset? ClosesAt,
    long TotalTrips);

/// <summary>What one bulkhead looks like right now.</summary>
/// <param name="Name">What it isolates.</param>
/// <param name="InUse">Slots occupied.</param>
/// <param name="Capacity">Slots total.</param>
/// <param name="Queued">Callers waiting for a slot.</param>
/// <param name="TotalRejections">Calls refused since the process started.</param>
public sealed record BulkheadState(
    string Name,
    int InUse,
    int Capacity,
    int Queued,
    long TotalRejections);

/// <summary>
/// Raised when a circuit is open.
/// <para>
/// A distinct type because the caller's handling is genuinely different: the dependency was not
/// called, so nothing happened, so a retry at a higher level is safe in a way that a retry after a
/// timeout is not. Collapsing this into a generic infrastructure exception loses that distinction
/// exactly where it matters — the payment path.
/// </para>
/// </summary>
public sealed class CircuitOpenException : Exception
{
    public CircuitOpenException(string dependency, DateTimeOffset closesAt)
        : base($"The circuit for '{dependency}' is open until {closesAt:O}. The dependency was not called.")
    {
        Dependency = dependency;
        ClosesAt = closesAt;
    }

    public CircuitOpenException()
    {
        Dependency = string.Empty;
    }

    public CircuitOpenException(string message) : base(message)
    {
        Dependency = string.Empty;
    }

    public CircuitOpenException(string message, Exception innerException) : base(message, innerException)
    {
        Dependency = string.Empty;
    }

    public string Dependency { get; }

    public DateTimeOffset ClosesAt { get; }
}

/// <summary>
/// Raised when a bulkhead is full.
/// <para>
/// Also distinct, and for the same reason: the call did not happen. A bulkhead rejection means the
/// platform protected itself, which is a different fact from the dependency failing, and an alert
/// that cannot tell them apart will page somebody about the wrong system.
/// </para>
/// </summary>
public sealed class BulkheadFullException : Exception
{
    public BulkheadFullException(string name, int capacity)
        : base($"The '{name}' bulkhead is full at {capacity} concurrent call(s). The operation was not attempted.")
    {
        Name = name;
        Capacity = capacity;
    }

    public BulkheadFullException()
    {
        Name = string.Empty;
    }

    public BulkheadFullException(string message) : base(message)
    {
        Name = string.Empty;
    }

    public BulkheadFullException(string message, Exception innerException) : base(message, innerException)
    {
        Name = string.Empty;
    }

    public string Name { get; }

    public int Capacity { get; }
}
