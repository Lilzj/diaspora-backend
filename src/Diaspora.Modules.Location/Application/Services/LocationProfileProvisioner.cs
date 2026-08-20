using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Location.Domain.Profiles;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Location.Application.Services;

/// <summary>
/// Gets the member's location profile, creating one the first time they need it.
/// <para>
/// Lazily rather than on registration, and that is a deliberate boundary decision. Creating a
/// profile at sign-up would mean Location subscribing to Identity's registration event and
/// writing a row for every account that never opens a location screen. Creating it on first
/// use costs one insert on a path that was already writing, and means Location needs to know
/// nothing about how members come into existence.
/// </para>
/// </summary>
public sealed class LocationProfileProvisioner
{
    private readonly ILocationProfileRepository _profiles;
    private readonly IUnitOfWork _unitOfWork;

    public LocationProfileProvisioner(
        ILocationProfileRepository profiles, IUnitOfWork unitOfWork)
    {
        _profiles = profiles;
        _unitOfWork = unitOfWork;
    }

    /// <summary>The member's profile, created if it does not exist yet.</summary>
    public async Task<LocationProfile> GetOrCreateAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var existing = await _profiles.GetByUserAsync(userId, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var profile = LocationProfile.CreateFor(userId);

        await _profiles.AddAsync(profile, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return profile;
    }

    /// <summary>The member's profile, or null. For read paths that must not write.</summary>
    public Task<LocationProfile?> FindAsync(
        UserId userId, CancellationToken cancellationToken = default) =>
        _profiles.GetByUserAsync(userId, cancellationToken);
}
