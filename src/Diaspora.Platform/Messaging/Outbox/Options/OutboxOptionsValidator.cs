using Microsoft.Extensions.Options;

namespace Diaspora.Platform.Messaging.Outbox.Options;

/// <summary>
/// The cross-field checks data annotations cannot express.
/// <para>
/// Every rule here describes a configuration that passes range validation and then behaves
/// incorrectly at three in the morning. Range attributes catch a typo; these catch a plausible
/// combination whose consequence is a duplicate publish, a stalled queue or a lost message —
/// and they run at startup, so the deployment fails instead of the delivery.
/// </para>
/// </summary>
public sealed class OutboxOptionsValidator : IValidateOptions<OutboxOptions>
{
    public ValidateOptionsResult Validate(string? name, OutboxOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        ValidateDispatch(options, failures);
        ValidateRetry(options, failures);
        ValidateRetention(options, failures);
        ValidateHealth(options, failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateDispatch(OutboxOptions options, List<string> failures)
    {
        var dispatch = options.Dispatch;

        // A publish that outlives its lease is being retried by a second worker while the first
        // is still running. At-least-once tolerates the duplicate; the bookkeeping does not —
        // the slow worker's eventual failure would overwrite the fast one's success.
        if (dispatch.PublishTimeoutSeconds >= dispatch.LeaseSeconds)
        {
            failures.Add(
                $"Platform:Outbox:Dispatch:PublishTimeoutSeconds ({dispatch.PublishTimeoutSeconds}) must be " +
                $"less than LeaseSeconds ({dispatch.LeaseSeconds}), or a publish can outlive its own lease " +
                "and be claimed by a second worker while it is still running.");
        }

        if (dispatch.LeaseRecoveryIntervalSeconds > dispatch.LeaseSeconds)
        {
            failures.Add(
                $"Platform:Outbox:Dispatch:LeaseRecoveryIntervalSeconds ({dispatch.LeaseRecoveryIntervalSeconds}) " +
                $"must not exceed LeaseSeconds ({dispatch.LeaseSeconds}), or a crashed worker's messages sit " +
                "unclaimable for longer than the lease they hold.");
        }

        // Concurrency inside a partition is precisely what partitioned ordering forbids.
        if (dispatch.Ordering != OutboxOrderingMode.None && dispatch.MaxConcurrency > 1)
        {
            failures.Add(
                $"Platform:Outbox:Dispatch:MaxConcurrency ({dispatch.MaxConcurrency}) is greater than 1 while " +
                $"Ordering is '{dispatch.Ordering}'. Concurrent publishing within a partition breaks the " +
                "ordering guarantee; partitions already run in parallel, so raise the batch size instead.");
        }

        if (dispatch.Ordering == OutboxOrderingMode.Global && dispatch.BatchSize > 1000)
        {
            failures.Add(
                "Platform:Outbox:Dispatch:BatchSize above 1000 under Global ordering claims a long serial run " +
                "that one failure stalls in its entirety. Lower the batch size or choose Partitioned ordering.");
        }
    }

    private static void ValidateRetry(OutboxOptions options, List<string> failures)
    {
        var retry = options.Retry;

        if (retry.ImmediateAttempts >= retry.MaxAttempts)
        {
            failures.Add(
                $"Platform:Outbox:Retry:ImmediateAttempts ({retry.ImmediateAttempts}) must be less than " +
                $"MaxAttempts ({retry.MaxAttempts}), or every retry is immediate and the backoff never applies.");
        }

        if (retry.InitialBackoffSeconds > retry.MaxBackoffSeconds)
        {
            failures.Add(
                $"Platform:Outbox:Retry:InitialBackoffSeconds ({retry.InitialBackoffSeconds}) exceeds " +
                $"MaxBackoffSeconds ({retry.MaxBackoffSeconds}).");
        }

        foreach (var poison in retry.PoisonExceptionTypes)
        {
            if (string.IsNullOrWhiteSpace(poison))
            {
                failures.Add(
                    "Platform:Outbox:Retry:PoisonExceptionTypes contains a blank entry. A blank pattern would " +
                    "match every exception and dead-letter the first failure of every message.");
            }
        }
    }

    private static void ValidateRetention(OutboxOptions options, List<string> failures)
    {
        var retention = options.Retention;

        // A dead letter purged before anyone replays it is the permanent loss this phase exists
        // to prevent, so it must outlive the delivered rows by a clear margin.
        if (retention.DeadLetterRetentionHours <= retention.DeliveredRetentionHours)
        {
            failures.Add(
                $"Platform:Outbox:Retention:DeadLetterRetentionHours ({retention.DeadLetterRetentionHours}) must " +
                $"exceed DeliveredRetentionHours ({retention.DeliveredRetentionHours}). A dead letter is " +
                "unfinished business; a delivered message is a receipt.");
        }

        if (!options.Retention.Enabled)
        {
            return;
        }

        var sweepHours = retention.SweepIntervalMinutes / 60d;

        if (sweepHours > retention.DeliveredRetentionHours)
        {
            failures.Add(
                $"Platform:Outbox:Retention:SweepIntervalMinutes ({retention.SweepIntervalMinutes}) is longer " +
                $"than DeliveredRetentionHours ({retention.DeliveredRetentionHours}), so the table grows " +
                "between sweeps faster than a sweep can collect it.");
        }
    }

    private static void ValidateHealth(OutboxOptions options, List<string> failures)
    {
        var health = options.Health;

        if (health.DegradedDepth >= health.UnhealthyDepth)
        {
            failures.Add(
                $"Platform:Outbox:Health:DegradedDepth ({health.DegradedDepth}) must be below UnhealthyDepth " +
                $"({health.UnhealthyDepth}).");
        }

        if (health.DegradedOldestPendingSeconds >= health.UnhealthyOldestPendingSeconds)
        {
            failures.Add(
                $"Platform:Outbox:Health:DegradedOldestPendingSeconds ({health.DegradedOldestPendingSeconds}) " +
                $"must be below UnhealthyOldestPendingSeconds ({health.UnhealthyOldestPendingSeconds}).");
        }
    }
}
