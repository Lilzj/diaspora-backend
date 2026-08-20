using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.Modules.Recommendations.Domain.Profiles;
using Diaspora.Persistence;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Recommendations.Persistence.Repositories;

/// <summary>
/// EF Core repository for the derived profile.
/// <para>
/// <b><see cref="GetOrCreateAsync"/> checks the change tracker before the database, and that is not
/// an optimisation.</b> One unit of work routinely folds several signals for one member — a batch of
/// client impressions, an event that produces three observations — and each asks for the profile.
/// Without the local lookup the second call would query, find nothing (the first is added but
/// unsaved), and add a second profile row that the unique index rejects at save time, failing the
/// whole batch.
/// </para>
/// <para>
/// <b>The three owned collections are always included.</b> A profile without its interests is a
/// profile that scores every candidate zero, and a lazy load here would be a silent one — the
/// aggregate would look complete and be empty.
/// </para>
/// </summary>
public sealed class PersonalizationProfileRepository : IPersonalizationProfileRepository
{
    private readonly DiasporaDbContext _dbContext;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public PersonalizationProfileRepository(
        DiasporaDbContext dbContext, IGuidGenerator guids, IClock clock)
    {
        _dbContext = dbContext;
        _guids = guids;
        _clock = clock;
    }

    private DbSet<PersonalizationProfile> Profiles => _dbContext.Set<PersonalizationProfile>();

    public async Task<PersonalizationProfile?> FindAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return await Profiles
            .Include(profile => profile.Interests)
            .Include(profile => profile.LocationAffinities)
            .Include(profile => profile.ActivitySummaries)
            .FirstOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);
    }

    public async Task<PersonalizationProfile> GetOrCreateAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        // The change tracker first. See the class summary — this is correctness, not speed.
        var tracked = _dbContext.ChangeTracker
            .Entries<PersonalizationProfile>()
            .Select(entry => entry.Entity)
            .FirstOrDefault(profile => profile.UserId == userId);

        if (tracked is not null)
        {
            return tracked;
        }

        if (await FindAsync(userId, cancellationToken) is { } existing)
        {
            return existing;
        }

        var created = PersonalizationProfile.Open(
            PersonalizationProfileId.From(_guids.NewSequentialGuid()), userId, _clock.UtcNow);

        await Profiles.AddAsync(created, cancellationToken);

        return created;
    }

    /// <summary>
    /// Profiles the sweep should rebuild, least recently built first.
    /// <para>
    /// Never-built profiles sort first because <c>null</c> orders before any value, which is the
    /// behaviour wanted: a profile that has never been built is the one most in need of it.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<PersonalizationProfile>> ListStaleAsync(
        DateTimeOffset staleBefore, int limit, CancellationToken cancellationToken = default) =>
        await Profiles
            .Include(profile => profile.Interests)
            .Include(profile => profile.LocationAffinities)
            .Include(profile => profile.ActivitySummaries)
            .Where(profile => profile.LastBuiltAt == null || profile.LastBuiltAt < staleBefore)
            .OrderBy(profile => profile.LastBuiltAt)
            .Take(Math.Max(1, limit))
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        Profiles.CountAsync(cancellationToken);

    public async Task<decimal> AverageCompletenessAsync(CancellationToken cancellationToken = default)
    {
        var any = await Profiles.AnyAsync(cancellationToken);

        if (!any)
        {
            return 0m;
        }

        var average = await Profiles.AverageAsync(
            profile => (double)profile.Completeness, cancellationToken);

        return decimal.Round((decimal)average, 2);
    }

    public void Update(PersonalizationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        RecommendationAggregateUpdater.MarkDirty(_dbContext, profile);
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

        // Removed through the change tracker rather than with ExecuteDelete, because the owned
        // collections are child tables and the tracker is what knows to remove them too.
        Profiles.Remove(existing);

        return true;
    }
}
