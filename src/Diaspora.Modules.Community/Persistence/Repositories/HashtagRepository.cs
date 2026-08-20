using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Hashtags;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Community.Persistence.Repositories;

/// <summary>EF Core repository for hashtags.</summary>
public sealed class HashtagRepository : IHashtagRepository
{
    private readonly DiasporaDbContext _dbContext;

    public HashtagRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<Hashtag> Hashtags => _dbContext.Set<Hashtag>();

    public async Task<Hashtag?> FindAsync(
        string slug, CancellationToken cancellationToken = default) =>
        await Hashtags.FirstOrDefaultAsync(hashtag => hashtag.Slug == slug, cancellationToken);

    public async Task<IReadOnlyList<Hashtag>> FindManyAsync(
        IReadOnlyCollection<string> slugs, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(slugs);

        if (slugs.Count == 0)
        {
            return [];
        }

        return await Hashtags
            .Where(hashtag => slugs.Contains(hashtag.Slug))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Hashtag>> SuggestAsync(
        string prefix, int limit, CancellationToken cancellationToken = default) =>
        await Hashtags
            .Where(hashtag =>
                hashtag.Status == HashtagStatus.Active && hashtag.Slug.StartsWith(prefix))
            .OrderByDescending(hashtag => hashtag.UsageCount)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Hashtag>> ListTrendingAsync(
        string? countryCode, int limit, CancellationToken cancellationToken = default)
    {
        var query = Hashtags.Where(hashtag =>
            hashtag.Status == HashtagStatus.Active && hashtag.RecentUsageCount > 0);

        if (!string.IsNullOrWhiteSpace(countryCode))
        {
            query = query.Where(hashtag => hashtag.PrimaryCountryCode == countryCode);
        }

        return await query
            .OrderByDescending(hashtag => hashtag.RecentUsageCount)
            .ThenByDescending(hashtag => hashtag.LastUsedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Hashtag>> ListForWindowRollAsync(
        int batchSize, CancellationToken cancellationToken = default) =>
        await Hashtags
            .Where(hashtag => hashtag.RecentUsageCount > 0)
            .OrderBy(hashtag => hashtag.LastUsedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Hashtag hashtag, CancellationToken cancellationToken = default) =>
        await Hashtags.AddAsync(hashtag, cancellationToken);
}
