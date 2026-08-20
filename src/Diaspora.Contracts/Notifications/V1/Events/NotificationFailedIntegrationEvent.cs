using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Notifications.V1.Events;

/// <summary>
/// A channel could not deliver the message.
/// <para>
/// Published on the <em>final</em> failure, not on every attempt — a retry ladder that announced
/// each rung would drown any consumer watching for genuine loss. <see cref="IsPermanent"/> separates
/// "the token is dead" from "we gave up after six tries".
/// </para>
/// </summary>
public sealed record NotificationFailedIntegrationEvent : IntegrationEvent
{
    public required Guid NotificationId { get; init; }

    public required Guid RecipientUserId { get; init; }

    public required string Channel { get; init; }

    public required string CategoryKey { get; init; }

    public required int Attempts { get; init; }

    /// <summary>A stable machine code from the channel — <c>NoEndpoint</c>, <c>Rejected</c>, <c>Timeout</c>.</summary>
    public required string ReasonCode { get; init; }

    /// <summary>True when retrying can never help. The endpoint is retired on a permanent failure.</summary>
    public required bool IsPermanent { get; init; }

    public required DateTimeOffset FailedAt { get; init; }
}
