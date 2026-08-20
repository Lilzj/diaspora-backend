using System.ComponentModel.DataAnnotations;

namespace Diaspora.Platform.Transactions;

/// <summary>
/// What the transaction subsystem can be told.
/// <para>
/// Deliberately small. Almost everything about transactional execution is a correctness property
/// rather than a preference, and a knob that lets a deployment turn correctness off is a knob
/// somebody will eventually turn. The only genuine choices here are how long a verification receipt
/// is worth keeping and how often to collect them.
/// </para>
/// </summary>
public sealed class TransactionReliabilityOptions
{
    public const string SectionName = "Platform:Transactions";

    /// <summary>
    /// How long a receipt is kept.
    /// <para>
    /// A receipt is worthless the moment its execution-strategy retry window closes, which is
    /// seconds. An hour is kept anyway, because a receipt costs almost nothing and being able to
    /// confirm that a transaction did commit — during the incident, not afterwards — is worth more
    /// than the rows.
    /// </para>
    /// </summary>
    [Range(1, 10080)]
    public int ReceiptRetentionMinutes { get; set; } = 60;

    /// <summary>
    /// Whether this deployment runs the receipt retention sweep at all.
    /// <para>
    /// On by default. Turning it off keeps every receipt for ever, which is a growing table rather
    /// than a correctness problem — receipts are read by identifier during an incident, never
    /// scanned.
    /// </para>
    /// <para>
    /// <b>Added in PSR-1 Final Certification.</b> This was the sixth of the loops with no switch
    /// and the only one in the platform kernel rather than a module — DEBT-049 recorded four, all
    /// of them modules', which is what re-deriving the set from the code rather than the ledger
    /// corrected.
    /// </para>
    /// </summary>
    public bool EnableReceiptRetentionSweep { get; set; } = true;

    /// <summary>How often the receipt sweep runs.</summary>
    [Range(1, 1440)]
    public int SweepIntervalMinutes { get; set; } = 15;

    /// <summary>
    /// Rows deleted per statement, so a first sweep against a table that accumulated while
    /// retention was misconfigured takes a series of short locks rather than one long one.
    /// </summary>
    [Range(1, 100000)]
    public int PurgeBatchSize { get; set; } = 5000;

    /// <summary>Ceiling on batches per sweep. What is left is collected next time.</summary>
    [Range(1, 10000)]
    public int MaxBatchesPerSweep { get; set; } = 20;
}
