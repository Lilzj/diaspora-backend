using Microsoft.EntityFrameworkCore.Storage;

namespace Diaspora.UnitTests.Persistence.Transactions;

/// <summary>
/// A retrying execution strategy for SQLite, so the suite can exercise the code path that only a
/// PostgreSQL deployment with <c>EnableRetryOnFailure</c> would otherwise reach.
/// <para>
/// <b>Without this, the whole of B1.0.2 would be untested.</b> SQLite's default strategy does not
/// retry, so <c>verifySucceeded</c> is never invoked, the receipt is never written and the replay
/// guard never fires — the suite would pass while proving nothing about the behaviour the patch
/// exists to provide. Configuring a retrying strategy is the supported EF Core seam for exactly
/// this, and it keeps the tests off Docker.
/// </para>
/// <para>
/// Retries are immediate. A real strategy backs off; a test that waited would only be slower.
/// </para>
/// </summary>
public sealed class FaultInjectingExecutionStrategy : ExecutionStrategy
{
    public FaultInjectingExecutionStrategy(ExecutionStrategyDependencies dependencies, int maxRetryCount)
        : base(dependencies, maxRetryCount, TimeSpan.FromSeconds(1))
    {
    }

    /// <summary>Only the injected marker is transient. Everything else fails the test loudly.</summary>
    protected override bool ShouldRetryOn(Exception exception) => exception is TransientTestException;

    protected override TimeSpan? GetNextDelay(Exception lastException) =>
        base.GetNextDelay(lastException) is null ? null : TimeSpan.Zero;
}
