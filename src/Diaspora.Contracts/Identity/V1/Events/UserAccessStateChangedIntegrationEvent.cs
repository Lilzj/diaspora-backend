using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Identity.V1.Events;

/// <summary>
/// Published on every movement along the progressive access ladder. This is the event
/// downstream modules should react to when deciding what a member may do.
/// </summary>
public sealed record UserAccessStateChangedIntegrationEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }

    public required string FromState { get; init; }

    public required string ToState { get; init; }

    public required string Reason { get; init; }
}
