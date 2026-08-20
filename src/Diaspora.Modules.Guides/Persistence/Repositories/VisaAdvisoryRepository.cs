using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.Modules.Guides.Domain.Visas;
using Diaspora.Persistence;
using Diaspora.Persistence.Paging;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Guides.Persistence.Repositories;

/// <summary>
/// EF Core repository for visa and immigration advisories.
/// <para>
/// <b>The nationality predicate is the one thing worth reading closely.</b> Asking for the rules that
/// apply to a Nigerian passport holder must return both the advisories naming Nigeria and those
/// naming nobody — a general "all visitors need a return ticket" rule applies to them as much as the
/// nationality-specific one. A repository that matched only the exact code would tell most members
/// that no rules exist for them, which is both wrong and the most dangerous kind of wrong this module
/// can be.
/// </para>
/// </summary>
public sealed class VisaAdvisoryRepository : IVisaAdvisoryRepository
{
    private readonly DiasporaDbContext _dbContext;

    public VisaAdvisoryRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<VisaAdvisory> Advisories => _dbContext.Set<VisaAdvisory>();

    public async Task<VisaAdvisory?> GetAsync(
        VisaAdvisoryId id, CancellationToken cancellationToken = default) =>
        await FullGraph(Advisories)
            .FirstOrDefaultAsync(advisory => advisory.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<VisaAdvisory?> GetIncludingArchivedAsync(
        VisaAdvisoryId id, CancellationToken cancellationToken = default) =>
        await FullGraph(Advisories.IgnoreQueryFilters())
            .FirstOrDefaultAsync(advisory => advisory.Id == id, cancellationToken);

    public async Task<CursorPagedResult<VisaAdvisory>> ListAsync(
        string countryCode,
        string? appliesTo,
        bool publishedOnly,
        bool activeOnly,
        DateTimeOffset asOf,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = Compose(countryCode, appliesTo, publishedOnly, activeOnly, asOf);
        var parsed = GuideCursor.Parse(cursor);

        if (parsed?.AsTimestamp() is { } after)
        {
            var tie = CursorBoundary.Exclusion(parsed.LastKey, VisaAdvisoryId.From);
            var seen = tie.Ids;
            var exhausted = tie.IsExhausted;

            query = query.Where(advisory =>
                advisory.CreatedAt < after
                || (advisory.CreatedAt == after && !exhausted && !seen.Contains(advisory.Id)));
        }

        var effective = Math.Clamp(limit, 1, IntelligenceLimits.MaxPageSize);

        var items = await FullGraph(query)
            .OrderByDescending(advisory => advisory.CreatedAt)
            .ThenByDescending(advisory => advisory.Id)
            .Take(effective + 1)
            .ToListAsync(cancellationToken);

        var hasMore = items.Count > effective;

        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        var next = hasMore && items.Count > 0
            ? GuideCursor.FromTimestamp(
                items[^1].CreatedAt,
                CursorBoundary.Key(
                    items,
                    advisory => advisory.CreatedAt,
                    advisory => advisory.Id.Value,
                    parsed?.LastKey,
                    parsed?.AsTimestamp())).Encode()
            : null;

        return CursorPagedResult<VisaAdvisory>.Create(items, next, hasMore);
    }

    public async Task<IReadOnlyList<VisaAdvisory>> TakeAsync(
        string countryCode,
        string? appliesTo,
        DateTimeOffset asOf,
        int limit,
        CancellationToken cancellationToken = default) =>
        await FullGraph(
            Compose(countryCode, appliesTo, publishedOnly: true, activeOnly: true, asOf))
            .OrderByDescending(advisory => advisory.EffectiveFrom)
            .ThenByDescending(advisory => advisory.CreatedAt)
            .ThenByDescending(advisory => advisory.Id)
            .Take(Math.Max(1, limit))
            .ToListAsync(cancellationToken);

    public async Task<int> CountAsync(
        string countryCode, DateTimeOffset asOf, CancellationToken cancellationToken = default) =>
        await Compose(
            countryCode, appliesTo: null, publishedOnly: true, activeOnly: true, asOf)
            .CountAsync(cancellationToken);

    public async Task AddAsync(
        VisaAdvisory advisory, CancellationToken cancellationToken = default) =>
        await Advisories.AddAsync(advisory, cancellationToken);

    /// <inheritdoc cref="Domain.Intelligence.IOfficialNoticeRepository.Remove" />
    public void Remove(VisaAdvisory advisory) => Advisories.Remove(advisory);

    private IQueryable<VisaAdvisory> Compose(
        string countryCode,
        string? appliesTo,
        bool publishedOnly,
        bool activeOnly,
        DateTimeOffset asOf)
    {
        var country = IntelligenceNormalization.Country(countryCode);

        if (country is null)
        {
            return Advisories.Where(_ => false);
        }

        var query = Advisories
            .AsNoTracking()
            .Where(advisory => advisory.CountryCode == country);

        var nationality = IntelligenceNormalization.Country(appliesTo);

        if (nationality is not null)
        {
            // The rules naming this nationality and the rules naming nobody. Both apply.
            query = query.Where(advisory =>
                advisory.AppliesToCountryCode == null
                || advisory.AppliesToCountryCode == nationality);
        }

        if (publishedOnly)
        {
            query = query.Where(advisory => advisory.Status == VisaAdvisoryStatus.Published);
        }

        if (activeOnly)
        {
            query = query.Where(advisory =>
                advisory.Status == VisaAdvisoryStatus.Published
                && (advisory.EffectiveFrom == null || advisory.EffectiveFrom <= asOf)
                && (advisory.ExpiresAt == null || advisory.ExpiresAt > asOf));
        }

        return query;
    }

    private static IQueryable<VisaAdvisory> FullGraph(IQueryable<VisaAdvisory> query) =>
        query
            .Include(advisory => advisory.Lines)
            .Include(advisory => advisory.RelatedGuides)
            .Include(advisory => advisory.Localizations);
}
