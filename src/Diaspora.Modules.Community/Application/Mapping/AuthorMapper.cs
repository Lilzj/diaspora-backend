using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Contracts.V1.Responses;
using Diaspora.Modules.Community.Domain.Comments;
using Diaspora.Modules.Community.Domain.Posts;

namespace Diaspora.Modules.Community.Application.Mapping;

/// <summary>
/// Projects the author block on a card.
/// <para>
/// <b>Community stores no member profile, and this mapper is where that shows.</b> It emits the
/// identifier, the publishing identity and whether the reader follows them — all of which are
/// B6's — and leaves the display name, avatar and reputation level null for a caller that has
/// not resolved them from Identity and Trust.
/// </para>
/// <para>
/// Null rather than a placeholder, deliberately. A projection that invented "Unknown" would make
/// an unresolved profile indistinguishable from a member who has not set a name, and the client
/// already knows how to render a member from an identifier.
/// </para>
/// <para>
/// Static, like every other stateless mapper in this folder. It holds nothing and resolves
/// nothing, so an injected instance would be a per-request allocation that carries no state.
/// </para>
/// </summary>
public static class AuthorMapper
{
    public static AuthorSummaryResponse ToSummary(CommunityPost post, ViewerContext viewer)
    {
        ArgumentNullException.ThrowIfNull(post);
        ArgumentNullException.ThrowIfNull(viewer);

        return new AuthorSummaryResponse
        {
            UserId = post.AuthorId.Value,
            IsFollowedByUser = viewer.FollowedMemberIds.Contains(post.AuthorId.Value),
            OrganizationId = post.Publisher.OrganizationId,
            OrganizationName = post.Publisher.DisplayName,
            PublishingIdentityType = CommunityWireNames.ToWire(post.Publisher.Type)
        };
    }

    public static AuthorSummaryResponse ToSummary(Comment comment, ViewerContext viewer)
    {
        ArgumentNullException.ThrowIfNull(comment);
        ArgumentNullException.ThrowIfNull(viewer);

        return new AuthorSummaryResponse
        {
            UserId = comment.AuthorId.Value,
            IsFollowedByUser = viewer.FollowedMemberIds.Contains(comment.AuthorId.Value)
        };
    }
}
