using System.Diagnostics.CodeAnalysis;
using Diaspora.Modules.Location.Domain.Areas;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.Search;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Location.Persistence.Repositories;

/// <summary>
/// EF Core repository for the geographic hierarchy.
/// <para>
/// Every traversal here is a predicate on the materialised path rather than a recursive
/// query. That is the whole payoff of <see cref="HierarchyPath"/>: ancestors are read off the
/// row, descendants are a prefix match, and the cost does not grow with the depth of the tree.
/// </para>
/// </summary>
public sealed class AdministrativeAreaRepository : IAdministrativeAreaRepository
{
    private readonly DiasporaDbContext _dbContext;

    public AdministrativeAreaRepository(DiasporaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private DbSet<AdministrativeArea> Areas => _dbContext.Set<AdministrativeArea>();

    public async Task<AdministrativeArea?> GetByIdAsync(
        AreaId id, CancellationToken cancellationToken = default) =>
        await Areas.FirstOrDefaultAsync(area => area.Id == id, cancellationToken);

    public async Task<AdministrativeArea?> GetBySlugAsync(
        LocationSlug slug,
        AdministrativeLevel? level = null,
        CountryCode? countryCode = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(slug);

        var query = Areas.AsNoTracking().Where(area => area.Slug == slug);

        if (level is not null)
        {
            query = query.Where(area => area.Level == level);
        }

        if (countryCode is not null)
        {
            query = query.Where(area => area.CountryCode == countryCode);
        }

        // Most prominent wins when a slug is ambiguous. That is what makes /peckham resolve
        // to the London one, which is the behaviour the frontend's existing links depend on.
        return await query
            .OrderByDescending(area => area.Prominence)
            .ThenBy(area => area.Name.Value)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdministrativeArea>> GetManyAsync(
        IReadOnlyCollection<AreaId> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (ids.Count == 0)
        {
            return [];
        }

        // Compared as the converted identifier rather than its inner Guid: EF applies the
        // value converter to the whole list, whereas a projection to .Value inside the
        // expression is a member access it cannot translate.
        var wanted = ids.Distinct().ToList();

        return await Areas
            .AsNoTracking()
            .Where(area => wanted.Contains(area.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdministrativeArea>> GetAncestorsAsync(
        AdministrativeArea area, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(area);

        // Ancestor paths are the proper prefixes of this node's path, and the path already
        // names all of them — so the whole breadcrumb is one IN clause rather than a walk of
        // one query per level.
        var ancestorPaths = new List<string>();
        var current = area.Path.Parent;

        while (current is not null)
        {
            ancestorPaths.Add(current.Value);
            current = current.Parent;
        }

        if (ancestorPaths.Count == 0)
        {
            return [];
        }

        var ancestors = await Areas
            .AsNoTracking()
            .Where(candidate => ancestorPaths.Contains(candidate.PathValue))
            .ToListAsync(cancellationToken);

        return GeographicHierarchy.ToBreadcrumb(ancestors);
    }

    public async Task<PagedResult<AdministrativeArea>> GetChildrenAsync(
        CountryId countryId,
        AreaId? parentId,
        AdministrativeLevel? level,
        PaginationRequest pagination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(countryId);
        ArgumentNullException.ThrowIfNull(pagination);

        var query = Areas.AsNoTracking().Where(area => area.CountryId == countryId);

        query = parentId is null
            ? query.Where(area => area.ParentId == null)
            : query.Where(area => area.ParentId == parentId);

        if (level is not null)
        {
            query = query.Where(area => area.Level == level);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(area => area.Prominence)
            .ThenBy(area => area.Name.Value)
            .Skip(pagination.Skip)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<AdministrativeArea>.Create(
            items, pagination.Page, pagination.PageSize, total);
    }

    public async Task<PagedResult<AdministrativeArea>> GetByLevelAsync(
        CountryCode countryCode,
        AdministrativeLevel level,
        string? term,
        PaginationRequest pagination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(countryCode);
        ArgumentNullException.ThrowIfNull(pagination);

        var query = Areas
            .AsNoTracking()
            .Where(area => area.CountryCode == countryCode && area.Level == level);

        if (!string.IsNullOrWhiteSpace(term))
        {
            var trimmed = term.Trim();
            query = query.Where(area => EF.Functions.Like(area.Name.Value, $"%{trimmed}%"));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(area => area.Prominence)
            .ThenBy(area => area.Name.Value)
            .Skip(pagination.Skip)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<AdministrativeArea>.Create(
            items, pagination.Page, pagination.PageSize, total);
    }

    [SuppressMessage(
        "Globalization", "CA1304:Specify CultureInfo",
        Justification = "Inside an expression tree, translated to SQL lower(); see FindByNameAsync.")]
    [SuppressMessage(
        "Globalization", "CA1311:Specify a culture or use an invariant version",
        Justification = "See CA1304.")]
    public async Task<IReadOnlyList<AdministrativeArea>> SearchAsync(
        LocationTextQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var candidates = Areas.AsNoTracking().AsQueryable();

        if (query.CountryCode is not null)
        {
            var code = query.CountryCode;
            candidates = candidates.Where(area => area.CountryCode == code);
        }

        if (query.Levels.Count > 0)
        {
            var levels = query.Levels.ToList();
            candidates = candidates.Where(area => levels.Contains(area.Level));
        }

        // Lowered on both sides. LIKE is case-sensitive on Postgres and not on the SQLite test
        // host, so "Port louis" found nothing in production and everything in the tests: a member
        // in Mauritius typing their own city in lower case was told it was not in the directory
        // (13 Oct 2026). Ranking below already ignored case; only this filter did not.
        var term = query.Term.ToLowerInvariant();

        candidates = candidates.Where(area =>
            EF.Functions.Like(area.Name.Value.ToLower(), $"%{term}%") ||
            (area.Name.LocalName != null && EF.Functions.Like(area.Name.LocalName.ToLower(), $"%{term}%")));

        // Over-fetch, then rank in memory. Ranking is a product decision — exact beats prefix
        // beats substring — and expressing it as a translatable ORDER BY would either be
        // provider-specific SQL or a CASE expression no index can serve. Four times the page
        // is a bounded read that lets the domain do the ordering it owns.
        return await candidates
            .OrderByDescending(area => area.Prominence)
            .Take(query.Limit * 4)
            .ToListAsync(cancellationToken);
    }

    [SuppressMessage(
        "Globalization", "CA1304:Specify CultureInfo",
        Justification =
            "The ToLower calls are inside an expression tree. They are never executed in .NET: EF "
            + "translates them to SQL lower(), which runs under the database collation. There is no "
            + "CultureInfo to specify, and passing one would fail to translate.")]
    [SuppressMessage(
        "Globalization", "CA1311:Specify a culture or use an invariant version",
        Justification = "See CA1304. ToLowerInvariant translates identically; neither is executed.")]
    [SuppressMessage(
        "Globalization", "CA1862:Use the StringComparison method overloads",
        Justification =
            "The StringComparison overloads have no translation on either provider, so they would "
            + "move the match into memory over the whole catalogue. Lowering both sides is what makes "
            + "it case-insensitive in SQL on Postgres and on the SQLite test host alike.")]
    public async Task<IReadOnlyList<AdministrativeArea>> FindByNameAsync(
        string name,
        IReadOnlyCollection<AdministrativeLevel> levels,
        CountryCode? countryCode,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(levels);

        if (string.IsNullOrWhiteSpace(name))
        {
            return [];
        }

        var term = name.Trim().ToLowerInvariant();
        var candidates = Areas.AsNoTracking().AsQueryable();

        if (countryCode is not null)
        {
            var code = countryCode;
            candidates = candidates.Where(area => area.CountryCode == code);
        }

        if (levels.Count > 0)
        {
            var wanted = levels.ToList();
            candidates = candidates.Where(area => wanted.Contains(area.Level));
        }

        // Equality, not Contains. See the contract: this places a row, and a substring match
        // would file every listing in London under Londonderry the moment one is seeded.
        candidates = candidates.Where(area =>
            area.Name.Value.ToLower() == term
            || (area.Name.LocalName != null && area.Name.LocalName.ToLower() == term));

        // Prominence breaks the tie between two real places of the same name. The member who
        // typed "Springfield" meant the one other people mean.
        return await candidates
            .OrderByDescending(area => area.Prominence)
            .Take(Math.Clamp(limit, 1, 50))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdministrativeArea>> GetWithinBoundsAsync(
        BoundingBox bounds,
        IReadOnlyCollection<AdministrativeLevel> levels,
        CountryCode? countryCode,
        HierarchyPath? withinPath,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bounds);
        ArgumentNullException.ThrowIfNull(levels);

        var query = Areas.AsNoTracking().Where(area => area.Center != null);

        if (countryCode is not null)
        {
            query = query.Where(area => area.CountryCode == countryCode);
        }

        if (withinPath is not null)
        {
            var prefix = withinPath.DescendantPrefix;
            query = query.Where(area => area.PathValue.StartsWith(prefix));
        }

        if (levels.Count > 0)
        {
            var levelList = levels.ToList();
            query = query.Where(area => levelList.Contains(area.Level));
        }

        query = query.Where(area =>
            area.Center!.Latitude <= bounds.North && area.Center.Latitude >= bounds.South);

        // The antimeridian case becomes an OR of two ordinary ranges rather than modular
        // arithmetic, so the coordinate index still applies. Fiji and Kiribati are real places
        // with real members.
        query = bounds.CrossesAntimeridian
            ? query.Where(area =>
                area.Center!.Longitude >= bounds.West || area.Center.Longitude <= bounds.East)
            : query.Where(area =>
                area.Center!.Longitude >= bounds.West && area.Center.Longitude <= bounds.East);

        return await query
            .OrderByDescending(area => area.Prominence)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdministrativeArea>> GetOutlinedOverlappingAsync(
        BoundingBox bounds,
        IReadOnlyCollection<AdministrativeLevel> levels,
        CountryCode? countryCode,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bounds);
        ArgumentNullException.ThrowIfNull(levels);

        var query = Areas.AsNoTracking().Where(area => area.Boundary != null && area.Extent != null);

        if (countryCode is not null)
        {
            query = query.Where(area => area.CountryCode == countryCode);
        }

        if (levels.Count > 0)
        {
            var levelList = levels.ToList();
            query = query.Where(area => levelList.Contains(area.Level));
        }

        // Two boxes overlap when neither lies wholly to one side of the other. The antimeridian is
        // an OR of two ranges, as it is for centres.
        query = query.Where(area =>
            area.Extent!.South <= bounds.North && area.Extent.North >= bounds.South);

        query = bounds.CrossesAntimeridian
            ? query.Where(area =>
                area.Extent!.East >= bounds.West || area.Extent.West <= bounds.East)
            : query.Where(area =>
                area.Extent!.West <= bounds.East && area.Extent.East >= bounds.West);

        // Not ordered by level in SQL: the level is stored as its name, and ordering names puts
        // "City" before "Region". Callers that care about depth sort the few rows this returns.
        return await query
            .OrderByDescending(area => area.Prominence)
            .ThenBy(area => area.PathValue)
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdministrativeArea>> GetDescendantsAsync(
        HierarchyPath path,
        IReadOnlyCollection<AdministrativeLevel> levels,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(levels);

        var prefix = path.DescendantPrefix;
        var query = Areas.AsNoTracking().Where(area => area.PathValue.StartsWith(prefix));

        if (levels.Count > 0)
        {
            var levelList = levels.ToList();
            query = query.Where(area => levelList.Contains(area.Level));
        }

        return await query
            .OrderByDescending(area => area.Prominence)
            .ThenBy(area => area.Name.Value)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> RewriteDescendantPathsAsync(
        AreaRelocation relocation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(relocation);

        var oldPrefix = relocation.PreviousDescendantPrefix;

        // Batched load-and-mutate rather than a set-based UPDATE. A single statement would be
        // faster, but the new path is a string built from the old one, and expressing that
        // through a value-converted property means provider-specific SQL — PostgreSQL's `||`
        // against SQL Server's `+` — which is exactly the coupling the rest of this module
        // avoids. Relocation happens when an administrative boundary is redrawn, on a scale of
        // decades, and the batching keeps even a large subtree off the large-object heap.
        const int batchSize = 500;
        var rewritten = 0;

        while (true)
        {
            // No Skip: a rewritten row no longer matches the old prefix, so the next batch is
            // always the first page of what is left. Paging with an offset would step over
            // the rows that just dropped out of the filter.
            var batch = await Areas
                .Where(area => area.PathValue.StartsWith(oldPrefix))
                .OrderBy(area => area.PathValue)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
            {
                break;
            }

            var changedInBatch = batch.Count(
                descendant => descendant.ApplyAncestorRelocation(relocation));

            // A batch where nothing moved would loop forever. It can only happen if the
            // rebased path exceeds the addressable depth, which is a data problem worth
            // surfacing rather than spinning on.
            if (changedInBatch == 0)
            {
                throw new InvalidOperationException(
                    $"Could not rebase {batch.Count} descendants of '{relocation.PreviousPath}'. " +
                    "The resulting hierarchy path would exceed the addressable depth.");
            }

            rewritten += changedInBatch;

            // Persisted per batch so the change tracker does not accumulate the whole subtree.
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return rewritten;
    }

    public async Task AddAsync(
        AdministrativeArea area, CancellationToken cancellationToken = default) =>
        await Areas.AddAsync(area, cancellationToken);

    public async Task<bool> AnyAsync(CancellationToken cancellationToken = default) =>
        await Areas.AnyAsync(cancellationToken);
}
