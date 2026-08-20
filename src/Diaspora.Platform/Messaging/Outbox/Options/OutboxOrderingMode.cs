namespace Diaspora.Platform.Messaging.Outbox.Options;

/// <summary>
/// How strictly the dispatcher preserves the order events were written in.
/// <para>
/// Ordering and throughput are the same dial turned in opposite directions, and the honest
/// thing is to make the trade explicit rather than pick one and describe it as "the" guarantee.
/// </para>
/// </summary>
public enum OutboxOrderingMode
{
    /// <summary>
    /// No ordering guarantee. Every eligible message is a candidate on every pass, and a
    /// message that fails does not hold anything back. Highest throughput; correct only where
    /// every consumer is order-independent.
    /// </summary>
    None = 0,

    /// <summary>
    /// <b>The default.</b> Messages sharing a partition key are delivered in write order and
    /// never concurrently; a message waiting on backoff blocks its own partition and nothing
    /// else. Unrelated aggregates proceed in parallel, and a stuck provider cannot stall the
    /// whole platform's notifications.
    /// </summary>
    Partitioned = 1,

    /// <summary>
    /// One global order across every module. Correct, slow, and almost never what is wanted:
    /// a single failing message halts delivery for the entire platform until it succeeds or
    /// dead-letters. Provided for deployments that genuinely require it.
    /// </summary>
    Global = 2
}
