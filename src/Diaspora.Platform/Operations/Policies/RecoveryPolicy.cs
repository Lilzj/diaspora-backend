namespace Diaspora.Platform.Operations.Policies;

/// <summary>
/// What the platform promises about losing and recovering a class of data.
/// <para>
/// RPO and RTO are usually written in a document nobody can test. Expressing them as a policy puts
/// them where the restore-validation job can read them, which is the difference between a recovery
/// objective and a recovery aspiration: the drill fails if the measured restore exceeds
/// <see cref="RecoveryTimeObjective"/>, and the failure names the policy.
/// </para>
/// <para>
/// <b>Retention is not backup.</b> <see cref="RetentionPeriod"/> is how long the live system keeps
/// the data; <see cref="BackupRetention"/> is how far back a restore can reach. A privacy erasure
/// that clears the live row and leaves ninety days of backups has not erased anything, which is
/// why the privacy audit reads both.
/// </para>
/// </summary>
public sealed record RecoveryPolicy : OperationalPolicy
{
    public const string PolicyKind = "recovery";

    public RecoveryPolicy(string name, int version = 1) : base(name, version)
    {
    }

    public override string Kind => PolicyKind;

    /// <summary>Maximum data loss tolerated, expressed as time. Sets the backup interval.</summary>
    public TimeSpan RecoveryPointObjective { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>Maximum time to restore service. The drill asserts against this.</summary>
    public TimeSpan RecoveryTimeObjective { get; init; } = TimeSpan.FromHours(4);

    /// <summary>How long the live system keeps this data before archive or deletion.</summary>
    public TimeSpan? RetentionPeriod { get; init; }

    /// <summary>How far back a restore can reach.</summary>
    public TimeSpan BackupRetention { get; init; } = TimeSpan.FromDays(30);

    /// <summary>Whether expired data is archived rather than deleted.</summary>
    public bool ArchiveOnExpiry { get; init; }

    /// <summary>
    /// How often a restore is actually performed and verified. An untested backup is a hypothesis;
    /// this is the interval at which the hypothesis is checked.
    /// </summary>
    public TimeSpan RestoreTestInterval { get; init; } = TimeSpan.FromDays(30);

    public override IEnumerable<string> Validate()
    {
        if (RecoveryPointObjective <= TimeSpan.Zero)
        {
            yield return "RecoveryPointObjective must be positive.";
        }

        if (RecoveryTimeObjective <= TimeSpan.Zero)
        {
            yield return "RecoveryTimeObjective must be positive.";
        }

        if (BackupRetention <= TimeSpan.Zero)
        {
            yield return "BackupRetention must be positive.";
        }

        if (BackupRetention < RecoveryPointObjective)
        {
            yield return "BackupRetention is shorter than the recovery point objective, so no restore can meet it.";
        }

        if (RetentionPeriod is { } retention && retention <= TimeSpan.Zero)
        {
            yield return "RetentionPeriod must be positive when set.";
        }

        if (RestoreTestInterval <= TimeSpan.Zero)
        {
            yield return "RestoreTestInterval must be positive.";
        }
    }
}
