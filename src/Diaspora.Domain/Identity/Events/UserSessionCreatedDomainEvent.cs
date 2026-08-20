using Diaspora.Domain.Identity.Devices;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.Events;

/// <summary>Raised when a new device session is opened.</summary>
public sealed record UserSessionCreatedDomainEvent(
    UserId UserId,
    SessionId SessionId,
    DeviceId? DeviceId,
    string DeviceName,
    DevicePlatform Platform,
    DateTimeOffset CreatedAt) : DomainEvent;
