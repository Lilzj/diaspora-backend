using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Location.Domain.Profiles;

/// <summary>The location profile's persistence boundary.</summary>
public interface ILocationProfileRepository
{
    /// <summary>
    /// The member's profile with its saved and recent locations loaded.
    /// <para>
    /// Eager rather than lazy because every caller needs them: the selector renders saved and
    /// recent together with the current location, and a profile fetched without them would
    /// only ever be followed by two more queries.
    /// </para>
    /// </summary>
    Task<LocationProfile?> GetByUserAsync(UserId userId, CancellationToken cancellationToken = default);

    Task AddAsync(LocationProfile profile, CancellationToken cancellationToken = default);
}
