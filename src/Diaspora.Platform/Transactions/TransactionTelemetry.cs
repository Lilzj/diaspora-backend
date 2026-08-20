using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Diaspora.Platform.Transactions;

/// <summary>
/// The transaction subsystem's instruments.
/// <para>
/// <b>Every counter here reports something that is invisible without it.</b> A retried transaction
/// succeeds, so nothing in the logs, the response, or the request duration says it happened twice.
/// A verified commit succeeds, so nothing says the platform came within one delegate of writing
/// everything twice. These are the only signals that a deployment's database connectivity is
/// degrading before it degrades enough to fail requests.
/// </para>
/// <para>
/// Counters, not logs. A retry storm produces thousands of these per minute, and the one thing
/// worse than no visibility is a log line per occurrence drowning the signal that mattered. The
/// two events rare enough to deserve a log — a verified commit and a refused replay — are logged
/// at warning and error respectively, and nothing else is logged at all.
/// </para>
/// </summary>
public static class TransactionTelemetry
{
    public const string SourceName = "Diaspora.Platform.Transactions";

    public const string Version = "1.0.0";

    public static readonly ActivitySource Activity = new(SourceName, Version);

    public static readonly Meter Meter = new(SourceName, Version);

    /// <summary>Transactions opened by the platform, tagged by whether the strategy retries.</summary>
    public static readonly Counter<long> Started = Meter.CreateCounter<long>(
        "diaspora.transactions.started",
        unit: "{transaction}",
        description: "Transactions opened by the unit of work.");

    /// <summary>Transactions committed, including those that took a retry to get there.</summary>
    public static readonly Counter<long> Committed = Meter.CreateCounter<long>(
        "diaspora.transactions.committed",
        unit: "{transaction}",
        description: "Transactions that committed.");

    /// <summary>Transactions rolled back, tagged by exception type.</summary>
    public static readonly Counter<long> RolledBack = Meter.CreateCounter<long>(
        "diaspora.transactions.rolled_back",
        unit: "{transaction}",
        description: "Transactions that rolled back, tagged by exception type.");

    /// <summary>
    /// Attempts the execution strategy made beyond the first.
    /// <para>
    /// The early warning. A steadily non-zero rate means the database connection is flaky and
    /// requests are still succeeding — which is exactly the window in which somebody can act
    /// before it stops being true.
    /// </para>
    /// </summary>
    public static readonly Counter<long> Replays = Meter.CreateCounter<long>(
        "diaspora.transactions.replays",
        unit: "{attempt}",
        description: "Execution-strategy retries of a platform transaction.");

    /// <summary>
    /// Times the platform asked the database whether a commit landed after losing the
    /// acknowledgement.
    /// </summary>
    public static readonly Counter<long> VerificationsRun = Meter.CreateCounter<long>(
        "diaspora.transactions.verifications",
        unit: "{verification}",
        description: "Commit verifications performed after a transient commit failure.");

    /// <summary>
    /// Commits that were confirmed to have landed despite the failure.
    /// <para>
    /// <b>Every one of these is a duplicate write that did not happen.</b> Before B1.0.2 the
    /// execution strategy would have replayed each of them against an already-committed
    /// transaction. This is the counter that says the patch is earning its keep.
    /// </para>
    /// </summary>
    public static readonly Counter<long> VerifiedCommits = Meter.CreateCounter<long>(
        "diaspora.transactions.verified_commits",
        unit: "{transaction}",
        description: "Commits confirmed by verification after a lost acknowledgement.");

    /// <summary>
    /// Replays the platform refused because the change tracker no longer held the work.
    /// <para>
    /// Should be rare. Non-zero means transient database failures are landing after a write has
    /// been accepted, and each one is a request that failed rather than a row written twice or
    /// lost — worth alerting on, and worth investigating the connection health behind it.
    /// </para>
    /// </summary>
    public static readonly Counter<long> RefusedReplays = Meter.CreateCounter<long>(
        "diaspora.transactions.refused_replays",
        unit: "{transaction}",
        description: "Retries declined because replaying would have run against an accepted change tracker.");

    /// <summary>Receipts removed by the retention sweep.</summary>
    public static readonly Counter<long> ReceiptsPurged = Meter.CreateCounter<long>(
        "diaspora.transactions.receipts_purged",
        unit: "{row}",
        description: "Transaction receipts removed by the retention sweep.");

    /// <summary>End-to-end duration of a platform transaction, including retries.</summary>
    public static readonly Histogram<double> Duration = Meter.CreateHistogram<double>(
        "diaspora.transactions.duration",
        unit: "ms",
        description: "Time a platform transaction took, including any retries.");
}
