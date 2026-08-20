using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Notifications.Domain.Identifiers;

/// <summary>
/// One delivery endpoint belonging to one member.
/// <para>
/// Distinct from <see cref="SharedKernel.Identity.DeviceId"/>, which is Identity's security record
/// of a signed-in device. This identifies the endpoint a message is routed to, correlated to
/// Identity's device by the client's own device string and never by primary key — because the two
/// have genuinely different lifetimes: a push token rotates without the device changing, and a
/// device is revoked without its token being retired.
/// </para>
/// </summary>
public sealed record DeviceRegistrationId(Guid Value) : StronglyTypedId(Value)
{
    public static DeviceRegistrationId New() => new(Guid.NewGuid());

    public static DeviceRegistrationId From(Guid value) => new(value);
}
