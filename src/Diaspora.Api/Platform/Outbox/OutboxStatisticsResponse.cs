namespace Diaspora.Api.Platform.Outbox;

/// <summary>
/// The queue at a glance.
/// <para>
/// <see cref="OldestPendingSeconds"/> leads deliberately. Depth answers "how much work is
/// there"; only the age of the oldest message answers "is any of it moving", and during an
/// incident that is the only question.
/// </para>
/// </summary>
public sealed record OutboxStatisticsResponse
{
    public required int Pending { get; init; }

    public required int Claimed { get; init; }

    /// <summary>Pending messages that have already failed at least once.</summary>
    public required int Retrying { get; init; }

    public required int Delivered { get; init; }

    public required int DeadLettered { get; init; }

    public required double OldestPendingSeconds { get; init; }

    public DateTimeOffset? OldestPendingAt { get; init; }

    public required DateTimeOffset ObservedAt { get; init; }

    /// <summary>Which module's events are backed up. Usually the answer on its own.</summary>
    public required IReadOnlyList<OutboxModuleStatisticsResponse> ByModule { get; init; }

    /// <summary>Which contract is backed up, for a module with one failing consumer of ten.</summary>
    public required IReadOnlyList<OutboxEventTypeStatisticsResponse> ByEventType { get; init; }
}
