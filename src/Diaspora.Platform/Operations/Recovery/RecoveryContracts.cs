namespace Diaspora.Platform.Operations.Recovery;

/// <summary>What a backup covers.</summary>
public enum BackupScope
{
    /// <summary>Everything.</summary>
    Full = 0,

    /// <summary>Changes since the last full backup.</summary>
    Differential = 1,

    /// <summary>The write-ahead log, which is what makes a point-in-time restore possible.</summary>
    Transactional = 2
}

/// <summary>One backup that exists.</summary>
/// <param name="Id">The backup's identifier, as its provider knows it.</param>
/// <param name="Scope">What it covers.</param>
/// <param name="TakenAt">When.</param>
/// <param name="SizeBytes">How large.</param>
/// <param name="Location">Where, as an opaque string. Never a credential.</param>
/// <param name="IsVerified">Whether a restore of this backup has actually been performed and checked.</param>
public sealed record BackupDescriptor(
    string Id,
    BackupScope Scope,
    DateTimeOffset TakenAt,
    long SizeBytes,
    string Location,
    bool IsVerified);

/// <summary>What a recovery drill measured.</summary>
/// <param name="Policy">Which <c>RecoveryPolicy</c> was being tested.</param>
/// <param name="BackupId">Which backup was restored.</param>
/// <param name="StartedAt">When the drill began.</param>
/// <param name="RestoreDuration">How long the restore took. Compared against the policy's RTO.</param>
/// <param name="DataLoss">
/// The gap between the backup and the moment of the simulated failure. Compared against the RPO.
/// </param>
/// <param name="Succeeded">Whether the restored data passed its checks.</param>
/// <param name="Findings">What went wrong, or what was noticed.</param>
public sealed record RecoveryDrillResult(
    string Policy,
    string BackupId,
    DateTimeOffset StartedAt,
    TimeSpan RestoreDuration,
    TimeSpan DataLoss,
    bool Succeeded,
    IReadOnlyList<string> Findings)
{
    /// <summary>Whether the drill met both objectives.</summary>
    public bool MeetsObjectives(TimeSpan recoveryTimeObjective, TimeSpan recoveryPointObjective) =>
        Succeeded && RestoreDuration <= recoveryTimeObjective && DataLoss <= recoveryPointObjective;
}

/// <summary>
/// Where backups come from.
/// <para>
/// <b>No cloud-specific implementation ships, and none should.</b> Every managed database takes
/// backups on a schedule the platform does not control and cannot improve on; writing a backup engine
/// here would produce a second, worse one. What the platform can do — and what nothing was doing — is
/// know which backups exist, check them against the recovery policy that claims to govern them, and
/// fail loudly when the two disagree.
/// </para>
/// <para>
/// The shipped implementation reports nothing and says so, which makes "no backup provider is
/// configured" a visible state on the dashboard rather than an assumption.
/// </para>
/// </summary>
public interface IBackupProvider
{
    /// <summary>The provider's name. <c>none</c> when nothing is configured.</summary>
    string Name { get; }

    /// <summary>Backups the provider knows about, newest first.</summary>
    Task<IReadOnlyList<BackupDescriptor>> ListAsync(
        int limit = 50, CancellationToken cancellationToken = default);

    /// <summary>The most recent backup, or null.</summary>
    Task<BackupDescriptor?> LatestAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Performs and measures a restore.
/// <para>
/// <b>An untested backup is a hypothesis.</b> The single most common disaster-recovery failure is not
/// a missing backup — it is a backup that has never been restored, discovered to be unusable at the
/// only moment it was needed. This is the contract that turns "we have backups" into a number with a
/// date on it.
/// </para>
/// <para>
/// A restore validator must never restore over the live system. That constraint cannot be expressed in
/// a type, which is why it is written here and enforced by the runbook and by review.
/// </para>
/// </summary>
public interface IRestoreValidator
{
    /// <summary>Which recovery policy this validates.</summary>
    string Policy { get; }

    /// <summary>Restores <paramref name="backupId"/> into an isolated target and checks it.</summary>
    Task<RecoveryDrillResult> DrillAsync(
        string backupId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Deletes or archives data that has passed its retention period.
/// <para>
/// Implemented by the module that owns the data, because only it knows what "expired" means for a row
/// and what has to happen to the things that reference it. The platform's part is the policy, the
/// schedule and the report.
/// </para>
/// </summary>
public interface IRetentionSweeper
{
    /// <summary>The owning module.</summary>
    string Module { get; }

    /// <summary>The <c>RecoveryPolicy</c> whose retention period governs this data.</summary>
    string PolicyName { get; }

    /// <summary>What is being swept, for the report.</summary>
    string DataSet { get; }

    /// <summary>
    /// Removes or archives anything older than the policy allows.
    /// </summary>
    /// <param name="olderThan">The cutoff, computed by the platform from the policy.</param>
    /// <param name="batchSize">
    /// Ceiling on one pass. A retention sweep that deletes a year of history in one statement holds
    /// locks long enough to be an outage.
    /// </param>
    /// <returns>How many records were removed or archived.</returns>
    Task<int> SweepAsync(
        DateTimeOffset olderThan, int batchSize, CancellationToken cancellationToken = default);
}
