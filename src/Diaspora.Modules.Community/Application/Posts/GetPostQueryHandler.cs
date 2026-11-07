using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Application.Mapping;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Contracts.V1.Responses;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Errors;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Polls;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Integration;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Application.Posts;

/// <summary>
/// Reads a post.
/// <para>
/// A post the caller may not see answers <b>not found</b> rather than forbidden. Saying "this
/// exists but is not for you" tells a caller the post exists, which is precisely what a narrow
/// audience withholds.
/// </para>
/// </summary>
public sealed class GetPostQueryHandler : IQueryHandler<GetPostQuery, CommunityPostResponse>
{
    private readonly ICommunityPostRepository _posts;
    private readonly IPollVoteRepository _votes;
    private readonly ICommunitySocialService _social;
    private readonly VisibilityService _visibility;
    private readonly ViewerContextResolver _viewers;
    private readonly IClock _clock;
    private readonly CommunityAuthorNames _authorNames;
    private readonly ICurrentUser _currentUser;

    public GetPostQueryHandler(
        ICommunityPostRepository posts,
        IPollVoteRepository votes,
        ICommunitySocialService social,
        VisibilityService visibility,
        ViewerContextResolver viewers,
        IClock clock,
        CommunityAuthorNames authorNames,
        ICurrentUser currentUser)
    {
        _posts = posts;
        _votes = votes;
        _social = social;
        _visibility = visibility;
        _viewers = viewers;
        _clock = clock;
        _authorNames = authorNames;
        _currentUser = currentUser;
    }

    public async Task<Result<CommunityPostResponse>> Handle(
        GetPostQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var post = await _posts.GetForReadAsync(
            CommunityPostId.From(request.PostId), cancellationToken);

        if (post is null)
        {
            return Result.Failure<CommunityPostResponse>(CommunityErrors.PostNotFound);
        }

        var viewer = await _viewers.ResolveAsync(cancellationToken);

        // Taken down by a moderation case: its author still reads it (to fix it, or to appeal), and
        // so do the staff deciding about it; for everybody else it is not there. Until 7 Nov 2026 the
        // permalink ignored the status, so a hidden or removed post stayed readable by anyone in
        // its audience who had the link.
        if (post.Status is PostStatus.Hidden or PostStatus.Removed
            && viewer.UserId != post.AuthorId
            && !_currentUser.Permissions.Contains(CommunityPermissions.ModerationRead, StringComparer.Ordinal))
        {
            return Result.Failure<CommunityPostResponse>(CommunityErrors.PostNotFound);
        }

        var visible = await _visibility.CanViewAsync(
            post.Reference,
            post.AuthorId.Value,
            post.Audience,
            viewer,
            post.Location.CountryCode,
            post.Location.AreaId,
            post.Location.HierarchyPath,
            post.Location.Latitude,
            post.Location.Longitude,
            cancellationToken);

        if (!visible)
        {
            return Result.Failure<CommunityPostResponse>(CommunityErrors.VisibilityDenied);
        }

        var social = await _social.GetSummariesAsync(
            [post.Reference], viewer.UserId, cancellationToken);

        Contracts.V1.Responses.PollResponse? poll = null;

        if (post.Poll is { } postPoll)
        {
            var votes = viewer.UserId is null
                ? []
                : (await _votes.GetForVoterAsync(postPoll.Id, viewer.UserId, cancellationToken))
                    .Select(vote => vote.OptionId)
                    .ToList();

            poll = PollMapper.ToResponse(postPoll, votes, _clock.UtcNow);
        }

        var names = await _authorNames.ResolveAsync([post.AuthorId], cancellationToken);

        return Result.Success(PostMapper.ToResponse(
            post, social.GetValueOrDefault(post.Reference.Key), viewer, poll, names: names));
    }
}
