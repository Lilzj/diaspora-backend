using System.Text;
using Diaspora.Platform.Messaging.Outbox.Administration;
using Diaspora.Platform.Messaging.Outbox.Model;
using Diaspora.Platform.Messaging.Outbox.Statistics;

namespace Diaspora.Api.Platform.Outbox;

/// <summary>
/// Turns the outbox's internal records into what the administration API returns.
/// <para>
/// A translation layer rather than serializing the domain types directly, for the same reason
/// every module has one: the internal shape is free to change, and an operator dashboard built
/// against it should not break when it does. It is also where the payload is dropped — the one
/// field that must not travel to a listing endpoint, and a rule that is easy to state in one
/// place and easy to forget in six.
/// </para>
/// </summary>
public static class OutboxResponseMapper
{
    public static OutboxMessageResponse ToResponse(this OutboxMessage message, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(message);

        return new OutboxMessageResponse
        {
            Sequence = message.Sequence,
            MessageId = message.MessageId,
            EventType = message.EventType,
            EventVersion = message.EventVersion,
            Module = message.Module,
            PartitionKey = message.PartitionKey,
            TenantId = message.TenantId,
            CorrelationId = message.CorrelationId,
            State = message.State.ToString(),
            AttemptCount = message.AttemptCount,
            OccurredAt = message.OccurredAt,
            EnqueuedAt = message.EnqueuedAt,
            NextAttemptAt = message.NextAttemptAt,
            LastAttemptAt = message.LastAttemptAt,
            AgeSeconds = Math.Max(0, (now - message.EnqueuedAt).TotalSeconds),
            LastError = message.LastError,
            LeaseOwner = message.LeaseOwner,
            LeaseExpiresAt = message.LeaseExpiresAt,
            PayloadBytes = Encoding.UTF8.GetByteCount(message.Payload)
        };
    }

    public static OutboxDeadLetterResponse ToResponse(this OutboxDeadLetter deadLetter)
    {
        ArgumentNullException.ThrowIfNull(deadLetter);

        return new OutboxDeadLetterResponse
        {
            Id = deadLetter.Id,
            MessageId = deadLetter.MessageId,
            EventType = deadLetter.EventType,
            EventVersion = deadLetter.EventVersion,
            Module = deadLetter.Module,
            PartitionKey = deadLetter.PartitionKey,
            TenantId = deadLetter.TenantId,
            CorrelationId = deadLetter.CorrelationId,
            Reason = deadLetter.Reason.ToString(),
            Error = deadLetter.Error,
            AttemptCount = deadLetter.AttemptCount,
            OccurredAt = deadLetter.OccurredAt,
            EnqueuedAt = deadLetter.EnqueuedAt,
            DeadLetteredAt = deadLetter.DeadLetteredAt,
            ReplayedAt = deadLetter.ReplayedAt
        };
    }

    public static OutboxDeliveryAttemptResponse ToResponse(this OutboxDeliveryAttempt attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        return new OutboxDeliveryAttemptResponse
        {
            AttemptNumber = attempt.AttemptNumber,
            StartedAt = attempt.StartedAt,
            DurationMilliseconds = Math.Round(attempt.DurationMilliseconds, 2),
            Succeeded = attempt.Succeeded,
            Error = attempt.Error,
            WorkerId = attempt.WorkerId
        };
    }

    public static OutboxStatisticsResponse ToResponse(this OutboxStatistics statistics)
    {
        ArgumentNullException.ThrowIfNull(statistics);

        return new OutboxStatisticsResponse
        {
            Pending = statistics.Pending,
            Claimed = statistics.Claimed,
            Retrying = statistics.Retrying,
            Delivered = statistics.Delivered,
            DeadLettered = statistics.DeadLettered,
            OldestPendingSeconds = Math.Round(statistics.OldestPendingAge.TotalSeconds, 1),
            OldestPendingAt = statistics.OldestPendingAt,
            ObservedAt = statistics.ObservedAt,
            ByModule = statistics.ByModule
                .Select(module => new OutboxModuleStatisticsResponse
                {
                    Module = module.Module,
                    Pending = module.Pending,
                    DeadLettered = module.DeadLettered,
                    OldestPendingAt = module.OldestPendingAt
                })
                .ToArray(),
            ByEventType = statistics.ByEventType
                .Select(type => new OutboxEventTypeStatisticsResponse
                {
                    EventType = type.EventType,
                    Pending = type.Pending,
                    DeadLettered = type.DeadLettered,
                    MaxAttempts = type.MaxAttempts
                })
                .ToArray()
        };
    }

    public static OutboxOperationResponse ToResponse(this OutboxOperationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new OutboxOperationResponse
        {
            Affected = result.Affected,
            Description = result.Description
        };
    }
}
