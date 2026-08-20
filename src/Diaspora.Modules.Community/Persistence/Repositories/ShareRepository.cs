using Diaspora.Modules.Community.Domain.Shares;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Community.Persistence.Repositories;

/// <summary>EF Core repository for shares.</summary>
public sealed class ShareRepository : IShareRepository
{
    private readonly DiasporaDbContext _dbContext;

    public ShareRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<Share> Shares => _dbContext.Set<Share>();

    public async Task<int> CountAsync(
        ContentReference content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        return await Shares.CountAsync(
            share => share.Content.Type == content.Type && share.Content.Id == content.Id,
            cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, int>> CountManyAsync(
        IReadOnlyCollection<ContentReference> contents,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contents);

        if (contents.Count == 0)
        {
            return new Dictionary<string, int>(StringComparer.Ordinal);
        }

        var ids = contents.Select(content => content.Id).Distinct().ToList();

        var rows = await Shares
            .Where(share => ids.Contains(share.Content.Id))
            .GroupBy(share => new { share.Content.Type, share.Content.Id })
            .Select(group => new { group.Key.Type, group.Key.Id, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            row => ContentReference.Create(row.Type, row.Id).Key,
            row => row.Count,
            StringComparer.Ordinal);
    }

    public async Task<IReadOnlyList<Share>> ListForContentAsync(
        ContentReference content, int limit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        return await Shares
            .Where(share => share.Content.Type == content.Type && share.Content.Id == content.Id)
            .OrderByDescending(share => share.SharedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Share>> ListByMemberAsync(
        UserId userId, int limit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return await Shares
            .Where(share => share.SharedBy == userId)
            .OrderByDescending(share => share.SharedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Share share, CancellationToken cancellationToken = default) =>
        await Shares.AddAsync(share, cancellationToken);
}
