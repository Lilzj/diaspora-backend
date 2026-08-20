using Diaspora.Domain.Identity.Devices;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.Events;

/// <summary>Raised the first time a physical device is seen for an account.</summary>
public sealed record UserDeviceRegisteredDomainEvent(
    UserId UserId,
    DeviceId DeviceId,
    string DeviceIdentifier,
    DevicePlatform Platform,
    DateTimeOffset RegisteredAt) : DomainEvent;
