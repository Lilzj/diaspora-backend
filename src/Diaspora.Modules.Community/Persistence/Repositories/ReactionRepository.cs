using Diaspora.Modules.Community.Domain.Reactions;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Community.Persistence.Repositories;

/// <summary>EF Core repository for reactions and their summaries.</summary>
public sealed class ReactionRepository : IReactionRepository
{
    private readonly DiasporaDbContext _dbContext;

    public ReactionRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<Reaction> Reactions => _dbContext.Set<Reaction>();

    private DbSet<ReactionSummary> Summaries => _dbContext.Set<ReactionSummary>();

    public async Task<Reaction?> FindAsync(
        ContentReference content,
        UserId userId,
        string reactionKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(userId);

        var key = Reaction.BuildKey(userId, content, reactionKey);

        return await Reactions.FirstOrDefaultAsync(
            reaction => reaction.UniquenessKey == key, cancellationToken);
    }

    public async Task<IReadOnlyList<Reaction>> ListForViewerAsync(
        IReadOnlyCollection<ContentReference> contents,
        UserId userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contents);
        ArgumentNullException.ThrowIfNull(userId);

        if (contents.Count == 0)
        {
            return [];
        }

        var ids = contents.Select(content => content.Id).Distinct().ToList();

        return await Reactions
            .Where(reaction => reaction.UserId == userId && ids.Contains(reaction.Content.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<ReactionSummary?> GetSummaryAsync(
        ContentReference content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        return await Summaries
            .Include(summary => summary.Tallies)
            .FirstOrDefaultAsync(summary => summary.ContentKey == content.Key, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, ReactionSummary>> GetSummariesAsync(
        IReadOnlyCollection<ContentReference> contents,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contents);

        if (contents.Count == 0)
        {
            return new Dictionary<string, ReactionSummary>(StringComparer.Ordinal);
        }

        var keys = contents.Select(content => content.Key).Distinct().ToList();

        var rows = await Summaries
            .Include(summary => summary.Tallies)
            .Where(summary => keys.Contains(summary.ContentKey))
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(summary => summary.ContentKey, StringComparer.Ordinal);
    }

    public async Task<int> CountAsync(
        ContentReference content, string reactionKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        return await Reactions.CountAsync(
            reaction =>
                reaction.ReactionKey == reactionKey &&
                reaction.Content.Type == content.Type &&
                reaction.Content.Id == content.Id,
            cancellationToken);
    }

    public async Task AddAsync(Reaction reaction, CancellationToken cancellationToken = default) =>
        await Reactions.AddAsync(reaction, cancellationToken);

    public async Task AddSummaryAsync(
        ReactionSummary summary, CancellationToken cancellationToken = default) =>
        await Summaries.AddAsync(summary, cancellationToken);

    public void Remove(Reaction reaction) => Reactions.Remove(reaction);
}
