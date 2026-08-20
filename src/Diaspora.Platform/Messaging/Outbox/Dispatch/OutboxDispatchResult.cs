namespace Diaspora.Platform.Messaging.Outbox.Dispatch;

/// <summary>
/// What one dispatcher pass did.
/// <para>
/// <see cref="Claimed"/> is the field the loop reads: a pass that filled its batch means there
/// is more waiting, so the next pass starts immediately instead of sleeping out the poll
/// interval. Without that, a backlog of ten thousand drains one batch per tick, and the queue
/// takes a hundred seconds to clear work the database could have handed over in one.
/// </para>
/// </summary>
/// <param name="Claimed">Messages leased this pass.</param>
/// <param name="Published">Messages a transport accepted.</param>
/// <param name="Failed">Messages a transport rejected.</param>
/// <param name="DeadLettered">Messages given up on.</param>
/// <param name="Deferred">
/// Messages claimed but not attempted, because an earlier message in the same partition failed
/// and ordering forbids overtaking it. Released back to pending, not lost.
/// </param>
public sealed record OutboxDispatchResult(
    int Claimed,
    int Published,
    int Failed,
    int DeadLettered,
    int Deferred)
{
    public static readonly OutboxDispatchResult Empty = new(0, 0, 0, 0, 0);

    /// <summary>Whether the pass took a full batch, and should therefore be followed immediately.</summary>
    public bool WasFull(int batchSize) => Claimed >= batchSize;
}
