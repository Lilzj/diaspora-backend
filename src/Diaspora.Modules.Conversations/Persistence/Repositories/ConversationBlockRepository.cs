using Diaspora.Modules.Conversations.Domain.Blocks;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Conversations.Persistence.Repositories;

/// <summary>The block list. Two columns and no joins, because it is read on every send.</summary>
public sealed class ConversationBlockRepository : IConversationBlockRepository
{
    private readonly DiasporaDbContext _dbContext;

    public ConversationBlockRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<ConversationBlock> Blocks => _dbContext.Set<ConversationBlock>();

    public Task<bool> AreBlockedAsync(
        UserId first, UserId second, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        // Either direction. Asking only "did the sender block the recipient" is the mistake that
        // lets a blocked member keep sending, and it reads as correct at the call site.
        return Blocks.AnyAsync(
            block =>
                (block.BlockerUserId == first && block.BlockedUserId == second)
                || (block.BlockerUserId == second && block.BlockedUserId == first),
            cancellationToken);
    }

    public async Task<ConversationBlock?> FindAsync(
        UserId blockerUserId, UserId blockedUserId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(blockerUserId);
        ArgumentNullException.ThrowIfNull(blockedUserId);

        return await Blocks.FirstOrDefaultAsync(
            block => block.BlockerUserId == blockerUserId && block.BlockedUserId == blockedUserId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<ConversationBlock>> ListForBlockerAsync(
        UserId blockerUserId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(blockerUserId);

        return await Blocks
            .Where(block => block.BlockerUserId == blockerUserId)
            .OrderByDescending(block => block.BlockedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlySet<Guid>> FilterBlockedAsync(
        UserId viewer, IReadOnlyCollection<UserId> others, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        ArgumentNullException.ThrowIfNull(others);

        if (others.Count == 0)
        {
            return new HashSet<Guid>();
        }

        // The strongly typed identifiers, not their Guid values. Both the Contains and the
        // projection have to stay at the value-object level: a member access through a
        // value-converted identifier is not translatable, and it fails at runtime with "could not be
        // translated" rather than at compile time.
        var ids = others.Distinct().ToList();

        var rows = await Blocks
            .Where(block =>
                (block.BlockerUserId == viewer && ids.Contains(block.BlockedUserId))
                || (block.BlockedUserId == viewer && ids.Contains(block.BlockerUserId)))
            .Select(block => new { Blocker = block.BlockerUserId, Blocked = block.BlockedUserId })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => row.Blocker == viewer ? row.Blocked.Value : row.Blocker.Value)
            .ToHashSet();
    }

    public void Add(ConversationBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);

        Blocks.Add(block);
    }

    public void Remove(ConversationBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);

        // Hard delete. A lifted block leaves no history — see ConversationBlock.AnnounceRemoval.
        Blocks.Remove(block);
    }
}
