using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Notifications.Domain.Events;

/// <summary>A member added or refreshed a delivery endpoint.</summary>
public sealed record DeviceRegisteredDomainEvent : DomainEvent
{
    public required DeviceRegistrationId DeviceRegistrationId { get; init; }

    public required UserId UserId { get; init; }

    public required DevicePlatformKind Platform { get; init; }

    public required NotificationChannelKind Channel { get; init; }

    public string? DeviceIdentifier { get; init; }

    /// <summary>False when the same endpoint was re-registered rather than newly added.</summary>
    public required bool IsNewRegistration { get; init; }

    public required DateTimeOffset RegisteredAt { get; init; }
}
