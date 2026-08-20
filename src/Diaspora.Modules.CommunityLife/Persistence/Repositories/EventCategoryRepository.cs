using Diaspora.Modules.CommunityLife.Domain.Catalog;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.CommunityLife.Persistence.Repositories;

/// <summary>
/// EF Core repository for the event-category catalogue.
/// <para>
/// <b>The facet read is filtered in memory rather than in SQL, and deliberately.</b> The catalogue
/// is a few dozen rows read on every faceted browse; a <c>LIKE '%religious%'</c> over a
/// comma-separated column cannot use the index anyway and would match <c>irreligious</c> if
/// somebody ever added it. Loading the small table and asking each row is both faster and correct.
/// </para>
/// </summary>
public sealed class EventCategoryRepository : IEventCategoryRepository
{
    private readonly DiasporaDbContext _dbContext;

    public EventCategoryRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<EventCategory> Categories => _dbContext.Set<EventCategory>();

    public async Task<EventCategory?> GetAsync(
        EventCategoryId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Categories.FirstOrDefaultAsync(
            category => category.Id == id, cancellationToken);
    }

    public async Task<EventCategory?> FindByKeyAsync(
        string? key, CancellationToken cancellationToken = default)
    {
        var normalized = Organization.NormalizeKey(key);

        return normalized is null
            ? null
            : await Categories.FirstOrDefaultAsync(
                category => category.Key == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<EventCategory>> ListAsync(
        bool includeRetired = false, CancellationToken cancellationToken = default)
    {
        var query = Categories.AsNoTracking();

        if (!includeRetired)
        {
            query = query.Where(category => category.IsActive);
        }

        return await query
            .OrderBy(category => category.Order)
            .ThenBy(category => category.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EventCategory>> ListByFacetAsync(
        string facet, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(facet))
        {
            return [];
        }

        // See the class summary: the whole catalogue, then an exact facet match per row.
        var all = await ListAsync(includeRetired: false, cancellationToken);

        return [.. all.Where(category => category.HasFacet(facet))];
    }

    public async Task AddAsync(
        EventCategory category, CancellationToken cancellationToken = default) =>
        await Categories.AddAsync(category, cancellationToken);

    public void Update(EventCategory category) => Categories.Update(category);
}
