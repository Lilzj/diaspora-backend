namespace Diaspora.Modules.Notifications.Contracts.V1.Responses;

/// <summary>One attempt, for the operator reading a failure timeline.</summary>
public sealed record NotificationDeliveryAttemptResponse
{
    public required Guid Id { get; init; }

    public required string Channel { get; init; }

    public required int AttemptNumber { get; init; }

    public required bool Succeeded { get; init; }

    public string? ReasonCode { get; init; }

    /// <summary>The transport's own words. Never shown to a member.</summary>
    public string? Detail { get; init; }

    public required DateTimeOffset AttemptedAt { get; init; }
}
