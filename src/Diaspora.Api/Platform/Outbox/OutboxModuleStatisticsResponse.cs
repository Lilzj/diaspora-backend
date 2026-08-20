namespace Diaspora.Api.Platform.Outbox;

/// <summary>One producing module's share of the queue.</summary>
public sealed record OutboxModuleStatisticsResponse
{
    public required string Module { get; init; }

    public required int Pending { get; init; }

    public required int DeadLettered { get; init; }

    public DateTimeOffset? OldestPendingAt { get; init; }
}
