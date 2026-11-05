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
        bool anyWord = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(start);

        // The names are stored normalised (lower case, plain letters, one space between words)
        // between bars, so the start of a name, or of a word inside one, is found without lowering
        // anything here.
        var marker = $"|{start}";
        var wordMarker = $" {start}";

        var query = _dbContext.Set<WorldTown>()
            .AsNoTracking()
            .Where(town => town.SearchNames.Contains(marker) || (anyWord && town.SearchNames.Contains(wordMarker)));

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
