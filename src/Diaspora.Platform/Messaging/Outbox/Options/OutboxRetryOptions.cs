using System.ComponentModel.DataAnnotations;

namespace Diaspora.Platform.Messaging.Outbox.Options;

/// <summary>
/// How hard, and for how long, the dispatcher tries before giving up.
/// <para>
/// The defaults span roughly forty minutes across eight attempts. That is long enough to ride
/// out a consumer restart, a database failover and a brief network partition, and short enough
/// that a genuinely broken message reaches the dead-letter queue while the deployment that
/// broke it is still the obvious suspect.
/// </para>
/// </summary>
public sealed class OutboxRetryOptions
{
    /// <summary>
    /// Attempts before the message is dead-lettered. Counted across process restarts, because
    /// the count lives in the row rather than in memory — a crash loop cannot reset the budget
    /// and retry a poison message forever.
    /// </summary>
    [Range(1, 100)]
    public int MaxAttempts { get; set; } = 8;

    /// <summary>
    /// How many of the first attempts retry with no delay at all.
    /// <para>
    /// Worth having, and worth keeping small. Most publish failures in a modular monolith are a
    /// transient database blip that is over in milliseconds, and waiting two seconds to discover
    /// that adds latency for nothing. Beyond one or two, immediate retries are just a tighter
    /// loop against something that is still down.
    /// </para>
    /// </summary>
    [Range(0, 10)]
    public int ImmediateAttempts { get; set; } = 1;

    /// <summary>The first backoff delay. Doubles from here.</summary>
    [Range(1, 600)]
    public int InitialBackoffSeconds { get; set; } = 2;

    /// <summary>
    /// The ceiling on a single backoff. Without one, attempt fifteen schedules itself for a
    /// week from Tuesday and the message is effectively lost with none of the visibility a dead
    /// letter would have given.
    /// </summary>
    [Range(1, 86400)]
    public int MaxBackoffSeconds { get; set; } = 600;

    /// <summary>
    /// Random proportion added to each delay, from 0 to 1.
    /// <para>
    /// Not decoration. A broker outage fails every in-flight message at the same instant, and
    /// an unjittered exponential backoff marches all of them back in one synchronized wave —
    /// which is how a recovering dependency gets knocked over a second time. A quarter is
    /// enough to smear the herd.
    /// </para>
    /// </summary>
    [Range(0d, 1d)]
    public double BackoffJitter { get; set; } = 0.25;

    /// <summary>
    /// Exception type names that can never succeed on a retry, matched against the exception's
    /// full type name and those of its inner exceptions.
    /// <para>
    /// The two the platform ships with are its own: an unreadable payload and an unknown
    /// contract. Both are decided by bytes already on disk, so spending eight attempts on them
    /// only delays the dead letter by forty minutes. A deployment may add its own — a validation
    /// exception from a consumer, for instance — but should be sparing: a type listed here gives
    /// up its entire retry budget.
    /// </para>
    /// </summary>
    public IList<string> PoisonExceptionTypes { get; } = [];
}
