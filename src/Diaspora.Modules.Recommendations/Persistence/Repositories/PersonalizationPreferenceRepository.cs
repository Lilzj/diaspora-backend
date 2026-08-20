using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.Modules.Recommendations.Domain.Preferences;
using Diaspora.Persistence;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Recommendations.Persistence.Repositories;

/// <summary>
/// EF Core repository for stated preferences.
/// <para>
/// <b><see cref="FindAsync"/> returning null is the common case and stays cheap on purpose.</b> Most
/// members never open the personalization settings screen, so most generations find nothing here.
/// The policy layer reads absence as "every default", which is why the read path has no
/// get-or-create — creating a row to answer a read would put one write per member per first feed on
/// the busiest path in the module.
/// </para>
/// </summary>
public sealed class PersonalizationPreferenceRepository : IPersonalizationPreferenceRepository
{
    private readonly DiasporaDbContext _dbContext;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public PersonalizationPreferenceRepository(
        DiasporaDbContext dbContext, IGuidGenerator guids, IClock clock)
    {
        _dbContext = dbContext;
        _guids = guids;
        _clock = clock;
    }

    private DbSet<PersonalizationPreference> Preferences =>
        _dbContext.Set<PersonalizationPreference>();

    public async Task<PersonalizationPreference?> FindAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return await Preferences
            .Include(preference => preference.Categories)
            .Include(preference => preference.BlockedTargets)
            .Include(preference => preference.Surfaces)
            .Include(preference => preference.FavoriteLocations)
            .FirstOrDefaultAsync(preference => preference.UserId == userId, cancellationToken);
    }

    public async Task<PersonalizationPreference> GetOrCreateAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var tracked = _dbContext.ChangeTracker
            .Entries<PersonalizationPreference>()
            .Select(entry => entry.Entity)
            .FirstOrDefault(preference => preference.UserId == userId);

        if (tracked is not null)
        {
            return tracked;
        }

        if (await FindAsync(userId, cancellationToken) is { } existing)
        {
            return existing;
        }

        var created = PersonalizationPreference.Open(
            PersonalizationPreferenceId.From(_guids.NewSequentialGuid()), userId, _clock.UtcNow);

        await Preferences.AddAsync(created, cancellationToken);

        return created;
    }

    public void Update(PersonalizationPreference preference)
    {
        ArgumentNullException.ThrowIfNull(preference);

        RecommendationAggregateUpdater.MarkDirty(_dbContext, preference);
    }

    public async Task<bool> DeleteForUserAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var existing = await FindAsync(userId, cancellationToken);

        if (existing is null)
        {
            return false;
        }

        Preferences.Remove(existing);

        return true;
    }
}
