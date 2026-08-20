using System.Collections.Concurrent;
using Diaspora.Contracts.Events;
using Diaspora.Platform.Messaging.Outbox.Abstractions;

namespace Diaspora.UnitTests.Platform.Outbox;

/// <summary>
/// A transport that remembers what it was asked to publish, and fails on demand.
/// <para>
/// Stands in for the in-process dispatcher and, eventually, for a broker. The failure hook is
/// the important half: most of what the dispatcher does only happens when a publish throws, and
/// a transport that always succeeds exercises none of it.
/// </para>
/// </summary>
public sealed class RecordingOutboxTransport : IOutboxTransport
{
    private readonly ConcurrentQueue<OutboxPublishContext> _published = new();

    /// <summary>Message identifiers this transport refuses, and what it throws for them.</summary>
    public Dictionary<Guid, Exception> Failures { get; } = [];

    /// <summary>When set, every publish waits this long — for exercising the publish timeout.</summary>
    public TimeSpan Delay { get; set; }

    /// <summary>
    /// When set, every publish blocks until its cancellation token fires, and never completes on
    /// its own.
    /// <para>
    /// <b>The deterministic way to exercise the publish timeout, and the reason DEBT-058 is
    /// closed.</b> The test that asserts a publish outliving its timeout is treated as a failure
    /// used to set <see cref="Delay"/> to five seconds against a one-second configured timeout and
    /// let the two race on the wall clock. Under full-solution load, with five test hosts running
    /// concurrently, the assertion could go either way — it failed once in six full runs while
    /// passing 3/3 in isolation, which is the signature of a test that measures the machine rather
    /// than the code.
    /// </para>
    /// <para>
    /// Blocking on the token instead removes the race without weakening the assertion. There is now
    /// exactly one thing that can complete this publish — the dispatcher's own timeout cancelling
    /// it — so the test still fails if the dispatcher stops applying a timeout, and it fails by
    /// hanging until the test runner's own limit rather than by flipping a comparison. A wider
    /// margin would have hidden the same defect; a fake clock would have needed the dispatcher to
    /// take one.
    /// </para>
    /// </summary>
    public bool BlockUntilCancelled { get; set; }

    public string Name => "recording";

    public IReadOnlyList<OutboxPublishContext> Published => _published.ToArray();

    public IReadOnlyList<long> PublishedSequences =>
        _published.Select(context => context.Sequence).ToArray();

    public async Task PublishAsync(
        IIntegrationEvent integrationEvent,
        OutboxPublishContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (BlockUntilCancelled)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        if (Delay > TimeSpan.Zero)
        {
            await Task.Delay(Delay, cancellationToken);
        }

        if (Failures.TryGetValue(context.MessageId, out var failure))
        {
            throw failure;
        }

        _published.Enqueue(context);
    }
}
