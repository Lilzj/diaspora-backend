using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Domain.Devices;

/// <summary>The delivery-endpoint aggregate's persistence port.</summary>
public interface IDeviceRegistrationRepository
{
    Task<DeviceRegistration?> GetAsync(
        DeviceRegistrationId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The endpoint a token already belongs to.
    /// <para>
    /// Looked up by token rather than by device string because a token can migrate between devices —
    /// a restored backup on a new phone — and registering it twice would push every message twice.
    /// </para>
    /// </summary>
    Task<DeviceRegistration?> FindByTokenAsync(
        UserId userId, string token, CancellationToken cancellationToken = default);

    Task<DeviceRegistration?> FindByDeviceIdentifierAsync(
        UserId userId, string deviceIdentifier, CancellationToken cancellationToken = default);

    /// <summary>Everything the member can currently be reached on, for one channel.</summary>
    Task<IReadOnlyList<DeviceRegistration>> ListDeliverableAsync(
        UserId userId, NotificationChannelKind channel, CancellationToken cancellationToken = default);

    /// <summary>Everything the devices screen lists, including suppressed endpoints.</summary>
    /// <summary>
    /// Every endpoint the member can currently be reached on, tracked — B13.3.
    /// <para>
    /// <b>Separate from <see cref="ListForUserAsync"/> because that one is a read.</b> The devices
    /// screen wants every row including the retired ones and does not write, so it is untracked. A
    /// command that retires them must have them tracked: an untracked aggregate re-attached for a
    /// write carries its own value of the concurrency token as the original, and the update is
    /// refused as a conflict against a row nobody else touched. That failure surfaces as a 409 on a
    /// button the member pressed once.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<DeviceRegistration>> ListDeliverableForUpdateAsync(
        UserId userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeviceRegistration>> ListForUserAsync(
        UserId userId, CancellationToken cancellationToken = default);

    Task<int> CountActiveAsync(UserId userId, CancellationToken cancellationToken = default);

    Task AddAsync(DeviceRegistration registration, CancellationToken cancellationToken = default);

    void Update(DeviceRegistration registration);
}
