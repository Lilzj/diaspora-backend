using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Community.Application.Mapping;
using Diaspora.Modules.Community.Contracts.V1.Responses;
using Diaspora.Modules.Community.Domain.Errors;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Polls;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Application.Polls;

/// <summary>
/// Casts a vote.
/// <para>
/// A member who has already voted has their vote <b>replaced</b> rather than refused, on a
/// single-choice poll. Changing your mind is a normal thing to do, and forcing a withdraw before
/// a re-vote makes a two-tap interaction out of a one-tap one.
/// </para>
/// </summary>
public sealed class CastVoteCommandHandler : ICommandHandler<CastVoteCommand, PollResponse>
{
    private readonly ICommunityPostRepository _posts;
    private readonly IPollVoteRepository _votes;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public CastVoteCommandHandler(
        ICommunityPostRepository posts,
        IPollVoteRepository votes,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _posts = posts;
        _votes = votes;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<PollResponse>> Handle(
        CastVoteCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return Result.Failure<PollResponse>(CommunityErrors.NotAuthenticated);
        }

        var post = await _posts.GetAsync(CommunityPostId.From(request.PostId), cancellationToken);

        if (post?.Poll is not { } poll)
        {
            return Result.Failure<PollResponse>(CommunityErrors.PollNotFound);
        }

        if (!poll.IsOpen(_clock.UtcNow))
        {
            return Result.Failure<PollResponse>(CommunityErrors.PollClosed);
        }

        var optionIds = request.Request.OptionIds
            .Distinct()
            .Select(PollOptionId.From)
            .ToList();

        if (optionIds.Count == 0)
        {
            return Result.Failure<PollResponse>(CommunityErrors.PollOptionNotFound);
        }

        if (optionIds.Count > 1 && !poll.AllowsMultiple)
        {
            return Result.Failure<PollResponse>(CommunityErrors.PollSingleChoice);
        }

        var existing = await _votes.GetForVoterAsync(poll.Id, userId, cancellationToken);
        var isNewVoter = existing.Count == 0;

        foreach (var previous in existing)
        {
            poll.WithdrawVote(previous.OptionId, voterHasNoRemainingVotes: false);
            _votes.Remove(previous);
        }

        foreach (var optionId in optionIds)
        {
            var registered = poll.RegisterVote(optionId, isNewVoter, _clock.UtcNow);

            if (registered.IsFailure)
            {
                return Result.Failure<PollResponse>(registered.Error);
            }

            var vote = PollVote.Cast(poll.Id, optionId, userId, _clock.UtcNow);

            vote.AddDomainEvent(new Domain.Events.PollVoteCastDomainEvent
            {
                PollId = poll.Id, OptionId = optionId, VoterId = userId
            });

            await _votes.AddAsync(vote, cancellationToken);

            isNewVoter = false;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(PollMapper.ToResponse(poll, optionIds, _clock.UtcNow));
    }
}
