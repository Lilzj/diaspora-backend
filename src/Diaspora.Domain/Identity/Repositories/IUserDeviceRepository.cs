using Diaspora.Domain.Identity.Devices;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Domain.Identity.Repositories;

/// <summary>
/// Persistence contract for the <see cref="UserDevice"/> aggregate.
/// </summary>
public interface IUserDeviceRepository
{
    Task<UserDevice?> GetByIdAsync(DeviceId deviceId, CancellationToken cancellationToken = default);

    Task<UserDevice?> GetByIdentifierAsync(
        UserId userId,
        string deviceIdentifier,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserDevice>> ListByUserAsync(UserId userId, CancellationToken cancellationToken = default);

    Task<int> CountByUserAsync(UserId userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// How many other accounts have registered this device identifier (D-13). A count: the accounts
    /// themselves are never read out.
    /// </summary>
    Task<int> CountOtherAccountsAsync(
        UserId userId,
        string deviceIdentifier,
        CancellationToken cancellationToken = default);

    Task AddAsync(UserDevice device, CancellationToken cancellationToken = default);

    void Update(UserDevice device);
}
