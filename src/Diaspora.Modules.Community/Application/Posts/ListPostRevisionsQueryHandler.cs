using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Application.Mapping;
using Diaspora.Modules.Community.Contracts.V1.Responses;
using Diaspora.Modules.Community.Domain.Errors;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Application.Posts;

/// <summary>Reads a post's revisions, newest first.</summary>
public sealed class ListPostRevisionsQueryHandler
    : IQueryHandler<ListPostRevisionsQuery, IReadOnlyList<PostRevisionResponse>>
{
    private readonly ICommunityPostRepository _posts;

    public ListPostRevisionsQueryHandler(ICommunityPostRepository posts) => _posts = posts;

    public async Task<Result<IReadOnlyList<PostRevisionResponse>>> Handle(
        ListPostRevisionsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var post = await _posts.GetAsync(CommunityPostId.From(request.PostId), cancellationToken);

        if (post is null)
        {
            return Result.Failure<IReadOnlyList<PostRevisionResponse>>(
                CommunityErrors.PostNotFound);
        }

        return Result.Success<IReadOnlyList<PostRevisionResponse>>(
            post.Revisions.Select(PostMapper.ToRevision).ToList());
    }
}
