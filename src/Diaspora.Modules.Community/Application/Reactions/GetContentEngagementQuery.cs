using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Application.Mapping;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Contracts.V1.Responses;
using Diaspora.Modules.Community.Integration;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Application.Reactions;

/// <summary>
/// Reads the reaction counts, the viewer's own reactions and the comment count on any content.
/// <para>
/// Added 13 Oct 2026 with official notices. Reacting already answered with the new counts, but
/// nothing could read them on first load for content other than a post, whose response carries
/// them — so a notice screen could add a reaction and never show one.
/// </para>
/// </summary>
[Authorize(Policy = CommunityPolicies.Read)]
public sealed record GetContentEngagementQuery : IQuery<ContentEngagementResponse>
{
    public required string ContentType { get; init; }

    public required Guid ContentId { get; init; }
}

public sealed class GetContentEngagementQueryHandler
    : IQueryHandler<GetContentEngagementQuery, ContentEngagementResponse>
{
    private readonly ContentReferenceResolver _references;
    private readonly ICommunitySocialService _social;
    private readonly ICurrentUser _currentUser;

    public GetContentEngagementQueryHandler(
        ContentReferenceResolver references, ICommunitySocialService social, ICurrentUser currentUser)
    {
        _references = references;
        _social = social;
        _currentUser = currentUser;
    }

    public async Task<Result<ContentEngagementResponse>> Handle(
        GetContentEngagementQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var reference = _references.Resolve(request.ContentType, request.ContentId);

        if (reference.IsFailure)
        {
            return Result.Failure<ContentEngagementResponse>(reference.Error);
        }

        var summaries = await _social.GetSummariesAsync(
            [reference.Value], _currentUser.UserId, cancellationToken);

        var summary = summaries.GetValueOrDefault(reference.Value.Key);

        return Result.Success(new ContentEngagementResponse
        {
            ContentType = CommunityWireNames.ToWire(reference.Value.Type),
            ContentId = reference.Value.Id,
            Reactions = ReactionMapper.ToResponse(summary),
            CommentCount = summary?.CommentCount ?? 0
        });
    }
}
