namespace Diaspora.Platform.Messaging.Outbox.Dispatch;

/// <summary>
/// Drains the outbox.
/// <para>
/// Deliberately not a <c>BackgroundService</c>. The scheduling — when to poll, how long to
/// wait, when to stop — is a hosting concern that differs between a web host, a worker process
/// and an integration test; the draining is not. Splitting them means the test suite drives
/// <see cref="DispatchOnceAsync"/> directly and asserts on the result, instead of starting a
/// timer and hoping.
/// </para>
/// </summary>
public interface IOutboxDispatcher
{
    /// <summary>
    /// Claims a batch and delivers it. Never throws for a delivery failure — a failed publish
    /// is recorded against the message and the pass continues.
    /// </summary>
    Task<OutboxDispatchResult> DispatchOnceAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns messages whose lease has lapsed to the pending pool. The crash-recovery path.
    /// </summary>
    Task<int> RecoverExpiredLeasesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes the shared statistics snapshot that gauges, health checks and the
    /// administration API read.
    /// </summary>
    Task RefreshStatisticsAsync(CancellationToken cancellationToken = default);
}
