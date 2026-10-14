using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.SharedKernel.Geography;

namespace Diaspora.Modules.Community.Infrastructure.Location;

/// <summary>
/// Tells the area page how many public community updates are pinned within a circle around a point.
/// <para>
/// Public, published and unexpired — the audience the nearby surface already contributes — and
/// measured at the post's own pin. A post whose author gave no pin is not within anything.
/// </para>
/// </summary>
public sealed class CommunityCircleCounter : ICircleResourceCounter
{
    private readonly ICommunityPostRepository _posts;

    public CommunityCircleCounter(ICommunityPostRepository posts) => _posts = posts;

    public string ResourceType => CommunityNearbyResourceProvider.Key;

    public async Task<CircleCount> CountWithinAsync(
        CircleArea circle, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(circle);

        var (posts, read) = await CommunityCircleReader.ReadAsync(
            _posts,
            new PostQueryFilter
            {
                Statuses = [PostStatus.Published],
                AllowedVisibility = [VisibilityLevel.Public],
                ExcludeExpired = true
            },
            circle,
            cancellationToken);

        return CircleCount.Of(
            posts.Count(post => circle.Contains(post.Location.Latitude, post.Location.Longitude)),
            read);
    }
}
