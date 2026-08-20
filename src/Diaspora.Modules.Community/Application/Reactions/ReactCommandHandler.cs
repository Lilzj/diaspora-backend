using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Application.Mapping;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Contracts.V1.Responses;
using Diaspora.Modules.Community.Domain.Errors;
using Diaspora.Modules.Community.Integration;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Application.Reactions;

/// <summary>
/// Reacts, and returns the new counts.
/// <para>
/// The counts come back on the same response so an optimistic client reconciles in one round
/// trip instead of refetching the card. That is the difference between a reaction that feels
/// instant and one that flickers.
/// </para>
/// </summary>
public sealed class ReactCommandHandler : ICommandHandler<ReactCommand, ReactionResponse>
{
    private readonly ReactionService _reactions;
    private readonly ContentReferenceResolver _references;
    private readonly ICommunitySocialService _social;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public ReactCommandHandler(
        ReactionService reactions,
        ContentReferenceResolver references,
        ICommunitySocialService social,
        ICurrentUser currentUser,
        IClock clock)
    {
        _reactions = reactions;
        _references = references;
        _social = social;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<ReactionResponse>> Handle(
        ReactCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return Result.Failure<ReactionResponse>(CommunityErrors.NotAuthenticated);
        }

        var reference = _references.Resolve(request.ContentType, request.ContentId);

        if (reference.IsFailure)
        {
            return Result.Failure<ReactionResponse>(reference.Error);
        }

        var outcome = request.Add
            ? await _reactions.ReactAsync(
                reference.Value, userId, request.ReactionKey, cancellationToken)
            : await _reactions.WithdrawAsync(
                reference.Value, userId, request.ReactionKey, cancellationToken);

        if (outcome.IsFailure)
        {
            return Result.Failure<ReactionResponse>(outcome.Error);
        }

        var social = await _social.GetSummariesAsync(
            [reference.Value], userId, cancellationToken);

        return Result.Success(new ReactionResponse
        {
            ReactionId = Guid.Empty,
            UserId = userId.Value,
            ContentType = CommunityWireNames.ToWire(reference.Value.Type),
            ContentId = reference.Value.Id,
            ReactionKey = request.ReactionKey,
            CreatedAt = _clock.UtcNow,
            Summary = ReactionMapper.ToResponse(social.GetValueOrDefault(reference.Value.Key))
        });
    }
}
