using Diaspora.Modules.Notifications.Domain.Devices;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Identifiers;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Notifications.Persistence.Repositories;

/// <summary>
/// EF Core repository for delivery endpoints.
/// <para>
/// <see cref="ListDeliverableAsync"/> is on the dispatcher's path for every push and is deliberately
/// narrow: active, not suppressed, not deleted. A registration a provider has rejected stays in the
/// table so the member can see it on the devices screen and so an operator can explain what happened,
/// and it must never be routed to again.
/// </para>
/// </summary>
public sealed class DeviceRegistrationRepository : IDeviceRegistrationRepository
{
    private readonly DiasporaDbContext _dbContext;

    public DeviceRegistrationRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<DeviceRegistration> Devices => _dbContext.Set<DeviceRegistration>();

    public async Task<DeviceRegistration?> GetAsync(
        DeviceRegistrationId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Devices.FirstOrDefaultAsync(device => device.Id == id, cancellationToken);
    }

    public async Task<DeviceRegistration?> FindByTokenAsync(
        UserId userId, string token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var trimmed = token.Trim();

        return await Devices.FirstOrDefaultAsync(
            device => device.UserId == userId && device.Token == trimmed, cancellationToken);
    }

    public async Task<DeviceRegistration?> FindByDeviceIdentifierAsync(
        UserId userId, string deviceIdentifier, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceIdentifier);

        var trimmed = deviceIdentifier.Trim();

        return await Devices.FirstOrDefaultAsync(
            device => device.UserId == userId && device.DeviceIdentifier == trimmed,
            cancellationToken);
    }

    public async Task<IReadOnlyList<DeviceRegistration>> ListDeliverableAsync(
        UserId userId, NotificationChannelKind channel, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return await Devices
            .AsNoTracking()
            .Where(device => device.UserId == userId)
            .Where(device => device.Channel == channel)
            .Where(device => device.IsActive && device.SuppressedAt == null)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Tracked, and that is the whole difference from <see cref="ListForUserAsync"/>. See the
    /// interface for why re-attaching an untracked aggregate turns an ordinary write into a 409.
    /// </summary>
    public async Task<IReadOnlyList<DeviceRegistration>> ListDeliverableForUpdateAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return await Devices
            .Where(device => device.UserId == userId)
            .Where(device => device.IsActive && device.SuppressedAt == null)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DeviceRegistration>> ListForUserAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return await Devices
            .AsNoTracking()
            .Where(device => device.UserId == userId)
            .OrderByDescending(device => device.LastActiveAt)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountActiveAsync(UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return Devices
            .AsNoTracking()
            .CountAsync(device => device.UserId == userId && device.IsActive, cancellationToken);
    }

    public async Task AddAsync(
        DeviceRegistration registration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registration);

        await Devices.AddAsync(registration, cancellationToken);
    }

    public void Update(DeviceRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        NotificationAggregateUpdater.MarkDirty(_dbContext, registration);
    }
}
