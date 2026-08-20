using Diaspora.Modules.Jobs.Domain.Employers;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Jobs.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Jobs.Persistence.Repositories;

/// <summary>
/// EF Core repository for employer profiles.
/// <para>
/// Every command read includes the members, because every employer-scoped command has to ask
/// "may this caller act here" and only the membership can answer. The directory read does not,
/// because a card renders a name, a badge and a country — and an employer with two hundred
/// recruiters must not cost two hundred rows to render one tile.
/// </para>
/// </summary>
public sealed class EmployerProfileRepository : IEmployerProfileRepository
{
    private readonly DiasporaDbContext _dbContext;

    public EmployerProfileRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<EmployerProfile> Employers => _dbContext.Set<EmployerProfile>();

    public async Task<EmployerProfile?> GetAsync(
        EmployerProfileId id, CancellationToken cancellationToken = default) =>
        await FullGraph(Employers)
            .FirstOrDefaultAsync(employer => employer.Id == id, cancellationToken);

    public async Task<EmployerProfile?> GetForReadAsync(
        EmployerProfileId id, CancellationToken cancellationToken = default) =>
        await FullGraph(Employers.AsNoTracking())
            .FirstOrDefaultAsync(employer => employer.Id == id, cancellationToken);

    public async Task<EmployerProfile?> GetBySlugAsync(
        JobSlug slug, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(slug);

        var value = slug.Value;

        return await FullGraph(Employers.AsNoTracking())
            .FirstOrDefaultAsync(employer => employer.Slug.Value == value, cancellationToken);
    }

    public async Task<IReadOnlyList<EmployerProfile>> GetManyAsync(
        IReadOnlyCollection<EmployerProfileId> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (ids.Count == 0)
        {
            return [];
        }

        // Whole identifiers: `.Value` has no column of its own, so EF cannot translate it.
        var keys = ids.ToList();

        // No members: this hydrates the employer chip on a page of job cards.
        return await Employers.AsNoTracking()
            .Where(employer => keys.Contains(employer.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EmployerProfile>> GetForMemberAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return await FullGraph(Employers.AsNoTracking())
            .Where(employer => employer.Members.Any(member => member.UserId == userId))
            .OrderBy(employer => employer.Identity.DisplayName)
            .ToListAsync(cancellationToken);
    }

    public async Task<EmployerProfile?> GetByVerificationIdAsync(
        Guid verificationId, CancellationToken cancellationToken = default) =>
        await FullGraph(Employers)
            .FirstOrDefaultAsync(
                employer => employer.TrustMark.VerificationId == verificationId,
                cancellationToken);

    public async Task<bool> SlugExistsAsync(
        JobSlug slug, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(slug);

        var value = slug.Value;

        return await Employers.IgnoreQueryFilters()
            .AnyAsync(employer => employer.Slug.Value == value, cancellationToken);
    }

    public async Task<CursorPagedResult<EmployerProfile>> QueryAsync(
        EmployerQueryFilter filter,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = Apply(Employers.AsNoTracking(), filter);

        var parsed = JobCursor.Parse(cursor);

        if (parsed?.LastKey is { Length: > 0 } lastSlug)
        {
            // Ordered by name with the slug as the tie-break, so the continuation is a strict
            // "after this". See JobCursor for why the slug rather than the key.
#pragma warning disable CA1309 // The ordinal overload is untranslatable. See JobPostingRepository.
            query = query.Where(employer => string.Compare(employer.Slug.Value, lastSlug) > 0);
#pragma warning restore CA1309
        }

        query = query
            .OrderBy(employer => employer.Identity.DisplayName)
            .ThenBy(employer => employer.Slug.Value);

        var effectiveLimit = Math.Clamp(limit, 1, JobLimits.MaxPageSize);
        var rows = await query.Take(effectiveLimit + 1).ToListAsync(cancellationToken);

        var hasMore = rows.Count > effectiveLimit;

        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var next = hasMore && rows.Count > 0
            ? JobCursor.Create(rows[^1].Identity.DisplayName, rows[^1].Slug.Value).Encode()
            : null;

        return CursorPagedResult<EmployerProfile>.Create(rows, next, hasMore);
    }

    public async Task AddAsync(
        EmployerProfile employer, CancellationToken cancellationToken = default) =>
        await Employers.AddAsync(employer, cancellationToken);

    public void Remove(EmployerProfile employer) => Employers.Remove(employer);

    private static IQueryable<EmployerProfile> FullGraph(IQueryable<EmployerProfile> query) =>
        query
            .Include(employer => employer.Members)
            .Include(employer => employer.HiringRegions);

    private IQueryable<EmployerProfile> Apply(
        IQueryable<EmployerProfile> query, EmployerQueryFilter filter)
    {
        // A candidate must never be shown an employer the platform has barred from recruiting.
        if (!filter.IncludeInactive)
        {
            query = query.Where(employer => employer.Status == EmployerStatus.Active);
        }

        if (filter.MemberId is { } memberId)
        {
            query = query.Where(employer =>
                employer.Members.Any(member => member.UserId == memberId));
        }

        if (!string.IsNullOrWhiteSpace(filter.CountryCode))
        {
            var country = filter.CountryCode.Trim().ToUpperInvariant();
            query = query.Where(employer => employer.PrimaryCountryCode == country);
        }

        // Matched against the declared hiring regions rather than where the employer is based:
        // a Berlin company hiring across the EU is exactly what a candidate in Lisbon wants.
        if (!string.IsNullOrWhiteSpace(filter.HiringIntoCountry))
        {
            var country = filter.HiringIntoCountry.Trim().ToUpperInvariant();

            query = query.Where(employer =>
                employer.PrimaryCountryCode == country
                || employer.HiringRegions.Any(region => region.CountryCode == country));
        }

        if (filter.OrganizationTypes is { Count: > 0 })
        {
            var types = filter.OrganizationTypes.ToList();
            query = query.Where(employer => types.Contains(employer.Identity.OrganizationType));
        }

        if (filter.Sizes is { Count: > 0 })
        {
            var sizes = filter.Sizes.ToList();
            query = query.Where(employer => sizes.Contains(employer.Identity.Size));
        }

        if (filter.VerifiedOnly)
        {
            query = query.Where(employer =>
                employer.TrustMark.State == JobVerificationState.Verified);
        }

        if (filter.SponsorsVisasOnly)
        {
            query = query.Where(employer =>
                employer.HiringRegions.Any(region => region.SponsorsVisas));
        }

        // Correlated against the posting set rather than a denormalised counter: an employer's
        // open-role count changes on every publish and expiry, and a column would be wrong more
        // often than right.
        if (filter.HiringNowOnly)
        {
            var open = new[]
            {
                JobStatus.Open,
                JobStatus.ReceivingApplications,
                JobStatus.Interviewing
            };

            var hiring = _dbContext.Set<JobPosting>()
                .Where(posting =>
                    open.Contains(posting.Status)
                    && posting.Visibility == JobVisibility.Public)
                .Select(posting => posting.EmployerId);

            query = query.Where(employer => hiring.Contains(employer.Id));
        }

        if (!string.IsNullOrWhiteSpace(filter.Text))
        {
            var term = filter.Text.Trim();

            query = query.Where(employer =>
                EF.Functions.Like(employer.Identity.DisplayName, $"%{term}%")
                || (employer.Identity.LegalName != null
                    && EF.Functions.Like(employer.Identity.LegalName, $"%{term}%")));
        }

        return query;
    }
}
