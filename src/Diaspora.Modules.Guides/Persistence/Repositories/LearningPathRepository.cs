using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Knowledge;
using Diaspora.Modules.Guides.Domain.LearningPaths;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Guides.Persistence.Repositories;

/// <summary>
/// EF Core repository for curated learning paths.
/// <para>
/// Every read includes the steps. A path without its steps is a title, and no caller in this module
/// wants one — which is why there is no card-shaped read here, unlike the guide repository where a
/// twenty-card page really would be several thousand rows.
/// </para>
/// </summary>
public sealed class LearningPathRepository : ILearningPathRepository
{
    private readonly DiasporaDbContext _dbContext;

    public LearningPathRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<LearningPath> Paths => _dbContext.Set<LearningPath>();

    public async Task<LearningPath?> GetAsync(
        LearningPathId id, CancellationToken cancellationToken = default) =>
        await Paths
            .Include(path => path.Steps)
            .FirstOrDefaultAsync(path => path.Id == id, cancellationToken);

    public async Task<LearningPath?> GetByKeyAsync(
        string countryCode, string key, CancellationToken cancellationToken = default)
    {
        var (country, normalizedKey) = Normalize(countryCode, key);

        return country is null || normalizedKey is null
            ? null
            : await Paths
                .Include(path => path.Steps)
                .FirstOrDefaultAsync(
                    path => path.CountryCode == country && path.Key == normalizedKey,
                    cancellationToken);
    }

    public async Task<LearningPath?> GetForReadAsync(
        string countryCode, string key, CancellationToken cancellationToken = default)
    {
        var (country, normalizedKey) = Normalize(countryCode, key);

        return country is null || normalizedKey is null
            ? null
            : await Paths
                .AsNoTracking()
                .Include(path => path.Steps)
                .FirstOrDefaultAsync(
                    path => path.CountryCode == country && path.Key == normalizedKey,
                    cancellationToken);
    }

    public async Task<IReadOnlyList<LearningPath>> ListByCountryAsync(
        string countryCode,
        bool publishedOnly = true,
        CancellationToken cancellationToken = default)
    {
        var country = KnowledgeNormalization.Country(countryCode);

        if (country is null)
        {
            return [];
        }

        var query = Paths
            .AsNoTracking()
            .Include(path => path.Steps)
            .Where(path => path.CountryCode == country);

        if (publishedOnly)
        {
            query = query.Where(path => path.Status == LearningPathStatus.Published);
        }

        return await query
            .OrderBy(path => path.Title)
            .ThenBy(path => path.Key)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> KeyExistsAsync(
        string countryCode, string key, CancellationToken cancellationToken = default)
    {
        var (country, normalizedKey) = Normalize(countryCode, key);

        return country is not null
            && normalizedKey is not null
            && await Paths.AnyAsync(
                path => path.CountryCode == country && path.Key == normalizedKey,
                cancellationToken);
    }

    public async Task AddAsync(LearningPath path, CancellationToken cancellationToken = default) =>
        await Paths.AddAsync(path, cancellationToken);

    private static (string? Country, string? Key) Normalize(string? countryCode, string? key) =>
        (KnowledgeNormalization.Country(countryCode),
            KnowledgeNormalization.PathKey(key));
}
