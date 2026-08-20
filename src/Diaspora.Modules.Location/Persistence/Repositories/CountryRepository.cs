using Diaspora.Modules.Location.Domain.Countries;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Location.Persistence.Repositories;

/// <summary>
/// EF Core repository over the shared <c>DiasporaDbContext</c>.
/// <para>
/// Reads are untracked; only <see cref="GetByCodeAsync"/> tracks, because it is the one the
/// command paths use. A tracked country loaded for a list of 195 is 195 change-tracker
/// entries nobody will ever save.
/// </para>
/// </summary>
public sealed class CountryRepository : ICountryRepository
{
    private readonly DiasporaDbContext _dbContext;

    public CountryRepository(DiasporaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private DbSet<Country> Countries => _dbContext.Set<Country>();

    public async Task<Country?> GetByCodeAsync(
        CountryCode code, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(code);

        return await Countries.FirstOrDefaultAsync(
            country => country.Code == code, cancellationToken);
    }

    public async Task<Country?> GetByIdAsync(
        CountryId id, CancellationToken cancellationToken = default) =>
        await Countries.FirstOrDefaultAsync(country => country.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Country>> ListAllAsync(
        CancellationToken cancellationToken = default) =>
        await Countries
            .AsNoTracking()
            .OrderByDescending(country => country.IsFeatured)
            .ThenBy(country => country.Name.Value)
            .ToListAsync(cancellationToken);

    public async Task<PagedResult<Country>> SearchAsync(
        string? term,
        bool? supportedOnly,
        string? continent,
        PaginationRequest pagination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pagination);

        var query = Countries.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(term))
        {
            var trimmed = term.Trim();

            // Code as well as name, so typing "GB" finds the United Kingdom. The selector's
            // search box is the only place most members ever type a country, and they type
            // both. The code candidate is resolved out here rather than inside the expression
            // so that an unparseable two-character term degrades to a name-only search instead
            // of throwing from inside a query.
            var codeCandidate = CountryCode.TryCreate(trimmed) is { IsSuccess: true } parsed
                ? parsed.Value
                : null;

            query = codeCandidate is null
                ? query.Where(country => EF.Functions.Like(country.Name.Value, $"%{trimmed}%"))
                : query.Where(country =>
                    EF.Functions.Like(country.Name.Value, $"%{trimmed}%") ||
                    country.Code == codeCandidate);
        }

        if (supportedOnly == true)
        {
            query = query.Where(country => country.IsSupported);
        }

        if (!string.IsNullOrWhiteSpace(continent))
        {
            var trimmed = continent.Trim();
            query = query.Where(country => country.Metadata.Continent == trimmed);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(country => country.IsFeatured)
            .ThenBy(country => country.Name.Value)
            .Skip(pagination.Skip)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<Country>.Create(items, pagination.Page, pagination.PageSize, total);
    }

    public async Task<bool> ExistsAsync(
        CountryCode code, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(code);

        return await Countries.AnyAsync(country => country.Code == code, cancellationToken);
    }

    public async Task AddAsync(Country country, CancellationToken cancellationToken = default) =>
        await Countries.AddAsync(country, cancellationToken);
}
