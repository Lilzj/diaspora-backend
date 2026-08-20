using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Notifications.V1.Events;

/// <summary>
/// A member added or refreshed a delivery endpoint.
/// <para>
/// <b>Not the same fact as Identity registering a device, and the difference matters.</b> Identity's
/// device record exists for session security — which browser signed in, is it trusted, revoke it.
/// This is a communication endpoint: a push token that rotates on its own schedule, can be rejected
/// by a provider, and is retired when it bounces. One member can have a trusted Identity device
/// with no usable endpoint, and an endpoint that outlives the session that created it.
/// </para>
/// <para>
/// The token itself never travels. It is a credential for someone else's service.
/// </para>
/// </summary>
public sealed record DeviceRegisteredIntegrationEvent : IntegrationEvent
{
    public required Guid DeviceRegistrationId { get; init; }

    public required Guid UserId { get; init; }

    /// <summary><c>Android</c>, <c>IOS</c>, <c>Web</c> or <c>Tablet</c>.</summary>
    public required string Platform { get; init; }

    /// <summary>The channel this endpoint serves. <c>Push</c> today.</summary>
    public required string Channel { get; init; }

    /// <summary>The client's own stable device string, so Identity's record can be correlated.</summary>
    public string? DeviceIdentifier { get; init; }

    /// <summary>False when the same endpoint was re-registered rather than newly added.</summary>
    public required bool IsNewRegistration { get; init; }

    public required DateTimeOffset RegisteredAt { get; init; }
}
