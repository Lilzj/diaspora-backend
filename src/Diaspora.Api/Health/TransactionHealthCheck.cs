using Diaspora.Persistence;
using Diaspora.Platform.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Diaspora.Api.Health;

/// <summary>
/// Reports how the transaction subsystem is configured, and whether its one table is being
/// collected.
/// <para>
/// <b>Deliberately almost never unhealthy.</b> Transactional execution has no runtime state that
/// can be "down": it either commits or it does not, per request, and a request that fails says so
/// in its own response. Failing readiness here would take an instance out of rotation for a
/// condition that removing it cannot improve.
/// </para>
/// <para>
/// What the check is actually for is answering two questions during an incident that are otherwise
/// awkward to answer from outside the process: <b>does this deployment's provider retry at all</b>
/// — which decides whether commit verification is even in play — and <b>is the receipt table being
/// collected</b>, since a growing one means the retention sweep has stopped and the fastest-growing
/// table in the platform is unattended.
/// </para>
/// </summary>
public sealed class TransactionHealthCheck : IHealthCheck
{
    /// <summary>
    /// Receipts above which retention is presumed broken.
    /// <para>
    /// A receipt lives about an hour by default. A backlog well past what an hour of traffic could
    /// produce means nothing is collecting them.
    /// </para>
    /// </summary>
    private const int DegradedReceiptCount = 1_000_000;

    private readonly DiasporaDbContext _context;
    private readonly ITransactionReceiptStore _receipts;
    private readonly TransactionReliabilityOptions _options;

    public TransactionHealthCheck(
        DiasporaDbContext context,
        ITransactionReceiptStore receipts,
        IOptions<TransactionReliabilityOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _context = context;
        _receipts = receipts;
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        var data = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            // The capability the platform keys its behaviour off, reported as the capability
            // rather than as a provider name.
            ["retriesOnFailure"] = strategy.RetriesOnFailure,
            ["commitVerification"] = strategy.RetriesOnFailure ? "enabled" : "not required",
            ["receiptRetentionMinutes"] = _options.ReceiptRetentionMinutes
        };

        if (!strategy.RetriesOnFailure)
        {
            // No retries means no replay, no verification and no receipts. Nothing to measure.
            return HealthCheckResult.Healthy(
                "Transactions execute once; the provider's strategy does not retry.", data);
        }

        var retained = await _receipts.CountAsync(cancellationToken);

        data["retainedReceipts"] = retained;

        return retained >= DegradedReceiptCount
            ? HealthCheckResult.Degraded(
                $"{retained:N0} transaction receipts are retained, well past a " +
                $"{_options.ReceiptRetentionMinutes}-minute window. The retention sweep has stopped.",
                data: data)
            : HealthCheckResult.Healthy(
                $"Commit verification is enabled; {retained:N0} receipt(s) retained.", data);
    }
}
