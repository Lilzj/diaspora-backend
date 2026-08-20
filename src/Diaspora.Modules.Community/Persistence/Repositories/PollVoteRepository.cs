using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Polls;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Community.Persistence.Repositories;

/// <summary>EF Core repository for poll votes.</summary>
public sealed class PollVoteRepository : IPollVoteRepository
{
    private readonly DiasporaDbContext _dbContext;

    public PollVoteRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<PollVote> Votes => _dbContext.Set<PollVote>();

    public async Task<IReadOnlyList<PollVote>> GetForVoterAsync(
        PollId pollId, UserId voterId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pollId);
        ArgumentNullException.ThrowIfNull(voterId);

        return await Votes
            .Where(vote => vote.PollId == pollId && vote.VoterId == voterId)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasVotedAsync(
        PollId pollId, UserId voterId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pollId);
        ArgumentNullException.ThrowIfNull(voterId);

        return await Votes.AnyAsync(
            vote => vote.PollId == pollId && vote.VoterId == voterId, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<PollId, IReadOnlyList<PollOptionId>>> GetVotesAsync(
        IReadOnlyCollection<PollId> pollIds,
        UserId voterId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pollIds);
        ArgumentNullException.ThrowIfNull(voterId);

        if (pollIds.Count == 0)
        {
            return new Dictionary<PollId, IReadOnlyList<PollOptionId>>();
        }

        var rows = await Votes
            .Where(vote => vote.VoterId == voterId && pollIds.Contains(vote.PollId))
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(vote => vote.PollId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PollOptionId>)group
                    .Select(vote => vote.OptionId)
                    .ToList());
    }

    public async Task AddAsync(PollVote vote, CancellationToken cancellationToken = default) =>
        await Votes.AddAsync(vote, cancellationToken);

    public void Remove(PollVote vote) => Votes.Remove(vote);
}
