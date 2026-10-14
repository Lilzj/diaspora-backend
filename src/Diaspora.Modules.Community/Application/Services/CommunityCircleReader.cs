using Diaspora.Modules.Community.Domain.Policies;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.SharedKernel.Geography;

namespace Diaspora.Modules.Community.Application.Services;

/// <summary>
/// Reads the posts inside the rectangle around a circle, page by page, up to the counting cap.
/// <para>
/// Through the repository's one query rather than a second, narrower one, so every filter rule the
/// feed obeys — audience, status, expiry — is obeyed here by construction. The caller applies the
/// exact circle.
/// </para>
/// </summary>
internal static class CommunityCircleReader
{
    public static async Task<(IReadOnlyList<CommunityPost> Posts, int Read)> ReadAsync(
        ICommunityPostRepository posts,
        PostQueryFilter filter,
        CircleArea circle,
        CancellationToken cancellationToken)
    {
        var boxed = filter with
        {
            BoundingBox = (circle.South, circle.West, circle.North, circle.East),
            Limit = CommunityLimits.MaxFeedPageSize,
            Cursor = null
        };

        var read = new List<CommunityPost>();

        while (read.Count < CircleCount.MaximumRowsRead)
        {
            var page = await posts.QueryAsync(boxed, cancellationToken);

            read.AddRange(page.Items);

            if (!page.HasMore || page.NextCursor is null)
            {
                break;
            }

            boxed = boxed with { Cursor = FeedCursor.Decode(page.NextCursor) };
        }

        return (read, read.Count);
    }
}
