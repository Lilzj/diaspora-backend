using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Community.Domain.Polls;

/// <summary>Reads and writes poll votes.</summary>
public interface IPollVoteRepository
{
    Task<IReadOnlyList<PollVote>> GetForVoterAsync(
        PollId pollId, UserId voterId, CancellationToken cancellationToken = default);

    Task<bool> HasVotedAsync(
        PollId pollId, UserId voterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Which polls, out of many, a member has voted in. The batch form exists because a feed
    /// page renders twenty cards and asking once per card is the shape that makes a poll feed
    /// twenty times slower than every other feed.
    /// </summary>
    Task<IReadOnlyDictionary<PollId, IReadOnlyList<PollOptionId>>> GetVotesAsync(
        IReadOnlyCollection<PollId> pollIds,
        UserId voterId,
        CancellationToken cancellationToken = default);

    Task AddAsync(PollVote vote, CancellationToken cancellationToken = default);

    void Remove(PollVote vote);
}
