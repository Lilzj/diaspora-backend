using Diaspora.Modules.Guides.Domain.Embassies;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Persistence;
using Diaspora.Persistence.Paging;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Guides.Persistence.Repositories;

/// <summary>
/// EF Core repository for the embassy directory.
/// <para>
/// Paging is on the mission's name, which is unique enough in practice to make ties rare and is
/// nonetheless tie-broken through <c>CursorBoundary</c> — because "rare" and "never" are different,
/// and a country with three consulates all called "Consulate General" is not a hypothetical.
/// </para>
/// <para>
/// The name is a string rather than a timestamp, so the cursor's sort key carries it directly rather
/// than through the timestamp helper. Sorting a directory by creation order would be alphabetical by
/// nothing anybody can see.
/// </para>
/// </summary>
public sealed class EmbassyRepository : IEmbassyRepository
{
    private readonly DiasporaDbContext _dbContext;

    public EmbassyRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<Embassy> Embassies => _dbContext.Set<Embassy>();

    public async Task<Embassy?> GetAsync(
        EmbassyId id, CancellationToken cancellationToken = default) =>
        await FullGraph(Embassies)
            .FirstOrDefaultAsync(embassy => embassy.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<Embassy?> GetIncludingArchivedAsync(
        EmbassyId id, CancellationToken cancellationToken = default) =>
        await FullGraph(Embassies.IgnoreQueryFilters())
            .FirstOrDefaultAsync(embassy => embassy.Id == id, cancellationToken);

    public async Task<CursorPagedResult<Embassy>> ListAsync(
        string? locatedIn,
        string? serving,
        bool verifiedOnly,
        string? text,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = Compose(locatedIn, serving, verifiedOnly, text);
        var parsed = GuideCursor.Parse(cursor);

        if (parsed is not null)
        {
            var tie = CursorBoundary.Exclusion(parsed.LastKey, EmbassyId.From);
            var seen = tie.Ids;
            var exhausted = tie.IsExhausted;
            var after = parsed.SortKey;

            query = query.Where(embassy =>
                string.Compare(embassy.Name, after, StringComparison.Ordinal) > 0
                || (embassy.Name == after && !exhausted && !seen.Contains(embassy.Id)));
        }

        var effective = Math.Clamp(limit, 1, Domain.Policies.IntelligenceLimits.MaxPageSize);

        var items = await FullGraph(query)
            .OrderBy(embassy => embassy.Name)
            .ThenBy(embassy => embassy.Id)
            .Take(effective + 1)
            .ToListAsync(cancellationToken);

        var hasMore = items.Count > effective;

        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        var next = hasMore && items.Count > 0
            ? GuideCursor.Create(items[^1].Name, BoundaryKey(items, parsed)).Encode()
            : null;

        return CursorPagedResult<Embassy>.Create(items, next, hasMore);
    }

    public async Task<IReadOnlyList<Embassy>> TakeAsync(
        string? locatedIn,
        string? serving,
        int limit,
        CancellationToken cancellationToken = default) =>
        await FullGraph(Compose(locatedIn, serving, verifiedOnly: false, text: null))
            .OrderByDescending(embassy => embassy.IsVerified)
            .ThenBy(embassy => embassy.Name)
            .ThenBy(embassy => embassy.Id)
            .Take(Math.Max(1, limit))
            .ToListAsync(cancellationToken);

    public async Task<int> CountAsync(
        string? locatedIn, string? serving, CancellationToken cancellationToken = default) =>
        await Compose(locatedIn, serving, verifiedOnly: false, text: null)
            .CountAsync(cancellationToken);

    public async Task<IReadOnlyList<Embassy>> GetByIdsAsync(
        IReadOnlyCollection<EmbassyId> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var distinct = ids.Distinct().ToList();

        return distinct.Count == 0
            ? []
            : await FullGraph(Embassies.AsNoTracking())
                .Where(embassy => distinct.Contains(embassy.Id))
                .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Embassy?> FindByVerificationCaseAsync(
        Guid verificationCaseId, CancellationToken cancellationToken = default) =>
        verificationCaseId == Guid.Empty
            ? null
            : await Embassies
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    embassy => embassy.VerificationCaseId == verificationCaseId,
                    cancellationToken);

    public async Task AddAsync(Embassy embassy, CancellationToken cancellationToken = default) =>
        await Embassies.AddAsync(embassy, cancellationToken);

    /// <inheritdoc cref="Domain.Intelligence.IOfficialNoticeRepository.Remove" />
    public void Remove(Embassy embassy) => Embassies.Remove(embassy);

    /// <summary>
    /// The identifiers the next page must skip: those on this page sharing its last name, plus any
    /// the incoming cursor already carried for that same name.
    /// <para>
    /// <b>Written here rather than through <c>CursorBoundary.Key</c>, and the reason is a type
    /// constraint rather than a difference of opinion.</b> That helper takes
    /// <c>TKey : struct, IEquatable&lt;TKey&gt;</c> and this listing sorts on a string. The obvious
    /// bridge — hashing the name to an integer — is silently broken in a way that would take a very
    /// long time to find: .NET randomises string hashing per process, so two requests served by two
    /// pods would compute different boundaries for the same name and the page after it would repeat
    /// or skip rows depending on which pod answered.
    /// </para>
    /// <para>
    /// The logic is otherwise identical, including abandoning a tie group that overflows rather than
    /// truncating it — a truncated group is re-served on the next page, which produces the same
    /// boundary, which produces the same page, forever.
    /// </para>
    /// </summary>
    private static string BoundaryKey(IReadOnlyList<Embassy> rows, GuideCursor? previous)
    {
        var boundary = rows[^1].Name;

        var carriedForward = previous is not null
            && string.Equals(previous.SortKey, boundary, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(previous.LastKey);

        if (carriedForward
            && string.Equals(
                previous!.LastKey, CursorBoundary.ExhaustedMarker, StringComparison.Ordinal))
        {
            return CursorBoundary.ExhaustedMarker;
        }

        var keys = new List<string>();

        if (carriedForward)
        {
            keys.AddRange(previous!.LastKey.Split(',', StringSplitOptions.RemoveEmptyEntries));
        }

        keys.AddRange(rows
            .Where(embassy => string.Equals(embassy.Name, boundary, StringComparison.Ordinal))
            .Select(embassy => embassy.Id.Value.ToString("N")));

        return keys.Count > CursorBoundary.MaxBoundaryIds
            ? CursorBoundary.ExhaustedMarker
            : string.Join(',', keys);
    }

    private IQueryable<Embassy> Compose(
        string? locatedIn, string? serving, bool verifiedOnly, string? text)
    {
        var query = Embassies.AsNoTracking();

        var located = IntelligenceNormalization.Country(locatedIn);

        if (located is not null)
        {
            query = query.Where(embassy => embassy.CountryCode == located);
        }

        var serves = IntelligenceNormalization.Country(serving);

        if (serves is not null)
        {
            query = query.Where(embassy => embassy.ServesCountryCode == serves);
        }

        if (verifiedOnly)
        {
            query = query.Where(embassy => embassy.IsVerified);
        }

        if (!string.IsNullOrWhiteSpace(text))
        {
            var term = text.Trim();

            query = query.Where(embassy =>
                EF.Functions.Like(embassy.Name, $"%{term}%")
                || (embassy.City != null && EF.Functions.Like(embassy.City, $"%{term}%")));
        }

        return query;
    }

    private static IQueryable<Embassy> FullGraph(IQueryable<Embassy> query) =>
        query
            .Include(embassy => embassy.OfficeHours)
            .Include(embassy => embassy.Services)
            .Include(embassy => embassy.Localizations);
}
