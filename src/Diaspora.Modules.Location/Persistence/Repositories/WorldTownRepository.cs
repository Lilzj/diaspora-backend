using Diaspora.Modules.Location.Domain.Towns;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Location.Persistence.Repositories;

public sealed class WorldTownRepository : IWorldTownRepository
{
    private readonly DiasporaDbContext _dbContext;

    public WorldTownRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlyList<WorldTown>> CandidatesAsync(
        string start,
        string? countryCode,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(start);

        // The names are stored normalised (lower case, plain letters) between bars, so the start
        // of any one of them is found without lowering anything here.
        var marker = $"|{start}";

        var query = _dbContext.Set<WorldTown>()
            .AsNoTracking()
            .Where(town => town.SearchNames.Contains(marker));

        if (!string.IsNullOrWhiteSpace(countryCode))
        {
            var country = countryCode.Trim().ToUpperInvariant();
            query = query.Where(town => town.CountryCode == country);
        }

        return await query
            .OrderByDescending(town => town.Population)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}
