using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Extensibility.Channels;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Extensibility.Devices;

/// <summary>
/// Where a member can be reached on a channel.
/// <para>
/// <b>A port over the module's own endpoint table, and that indirection is the point.</b> Today the
/// answer comes from <c>DeviceRegistration</c> rows this module owns. A deployment that moves push
/// subscription into a provider's topic service — where the platform holds no tokens at all —
/// replaces this and changes nothing in the dispatcher. The brief calls that "future provider
/// independence".
/// </para>
/// <para>
/// The dispatcher only ever asks this interface; it never touches the endpoint repository directly.
/// An architecture test enforces that.
/// </para>
/// </summary>
public interface IDeviceRegistry
{
    /// <summary>Every usable endpoint for a member on one channel. Empty is an ordinary answer.</summary>
    Task<IReadOnlyList<ChannelEndpoint>> ResolveAsync(
        UserId userId,
        NotificationChannelKind channel,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// A provider said these endpoints are gone.
    /// <para>
    /// Called after a send, including a successful one — a multicast commonly succeeds for three
    /// devices and reports the fourth unregistered.
    /// </para>
    /// </summary>
    Task RetireAsync(
        IReadOnlyList<Guid> endpointIds,
        string reasonCode,
        CancellationToken cancellationToken = default);

    /// <summary>A message reached an endpoint. Clears its failure count.</summary>
    Task RecordSuccessAsync(
        IReadOnlyList<Guid> endpointIds, CancellationToken cancellationToken = default);
}
