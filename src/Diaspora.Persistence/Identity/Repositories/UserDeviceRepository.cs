using Diaspora.Domain.Identity.Devices;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Persistence.Identity.Repositories;

/// <inheritdoc cref="IUserDeviceRepository" />
public sealed class UserDeviceRepository : IUserDeviceRepository
{
    private readonly DiasporaDbContext _dbContext;

    public UserDeviceRepository(DiasporaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private DbSet<UserDevice> Devices => _dbContext.Set<UserDevice>();

    public async Task<UserDevice?> GetByIdAsync(DeviceId deviceId, CancellationToken cancellationToken = default) =>
        await Devices.FirstOrDefaultAsync(device => device.Id == deviceId, cancellationToken);

    public async Task<UserDevice?> GetByIdentifierAsync(
        UserId userId,
        string deviceIdentifier,
        CancellationToken cancellationToken = default) =>
        await Devices.FirstOrDefaultAsync(
            device => device.UserId == userId && device.DeviceIdentifier == deviceIdentifier,
            cancellationToken);

    public async Task<IReadOnlyList<UserDevice>> ListByUserAsync(
        UserId userId,
        CancellationToken cancellationToken = default) =>
        await Devices
            .Where(device => device.UserId == userId)
            .OrderByDescending(device => device.LastActiveAt)
            .ToListAsync(cancellationToken);

    public async Task<int> CountByUserAsync(UserId userId, CancellationToken cancellationToken = default) =>
        await Devices.CountAsync(device => device.UserId == userId, cancellationToken);

    public async Task AddAsync(UserDevice device, CancellationToken cancellationToken = default) =>
        await Devices.AddAsync(device, cancellationToken);

    public void Update(UserDevice device) =>
        TrackedEntityUpdater.MarkModified(_dbContext, Devices, device);
}
