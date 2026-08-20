using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Contracts.V1.Responses;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Polls;

namespace Diaspora.Modules.Community.Application.Mapping;

/// <summary>
/// Projects polls.
/// <para>
/// When results are hidden the counts are <b>null rather than zero</b>. Zero is a claim that
/// nobody voted; null is the truth, which is that the reader may not know yet.
/// </para>
/// </summary>
public static class PollMapper
{
    public static PollResponse ToResponse(
        Poll poll,
        IReadOnlyCollection<PollOptionId> viewerVotes,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(poll);
        ArgumentNullException.ThrowIfNull(viewerVotes);

        var hasVoted = viewerVotes.Count > 0;
        var canSee = poll.CanSeeResults(hasVoted, now);
        var total = poll.TotalVotes;

        return new PollResponse
        {
            PollId = poll.Id.Value,
            Question = poll.Question,
            Options = poll.Options
                .Select(option => new PollOptionResponse
                {
                    OptionId = option.Id.Value,
                    Label = option.Label,
                    Position = option.Position,
                    ImageMediaId = option.ImageMediaId?.Value,
                    VoteCount = canSee ? option.VoteCount : null,
                    SharePercent = canSee && total > 0
                        ? Math.Round(option.VoteCount * 100d / total, 1)
                        : canSee ? 0d : null,
                    VotedByViewer = viewerVotes.Contains(option.Id)
                })
                .ToList(),
            AllowsMultiple = poll.AllowsMultiple,
            ResultVisibility = CommunityWireNames.ToWire(poll.ResultVisibility),
            Status = CommunityWireNames.ToWire(poll.Status),
            ClosesAt = poll.ClosesAt,
            TotalVotes = canSee ? total : null,
            VoterCount = canSee ? poll.VoterCount : null,
            HasVoted = hasVoted,
            CanSeeResults = canSee
        };
    }
}
