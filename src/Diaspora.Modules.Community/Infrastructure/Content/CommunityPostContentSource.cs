using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Community.Extensibility.Content;

namespace Diaspora.Modules.Community.Infrastructure.Content;

/// <summary>
/// Community's own content source, for its posts.
/// <para>
/// Registered like any other module's would be, and that is the point: the seam other modules
/// implement is the same one this module uses for itself. A seam its own author does not use is
/// a seam nobody has tested.
/// </para>
/// </summary>
public sealed class CommunityPostContentSource : ICommunityContentSource
{
    private readonly ICommunityPostRepository _posts;

    public CommunityPostContentSource(ICommunityPostRepository posts) => _posts = posts;

    public CommunityContentType ContentType => CommunityContentType.Post;

    public async Task<ContentDescriptor?> DescribeAsync(
        Guid contentId, CancellationToken cancellationToken = default)
    {
        var post = await _posts.GetForReadAsync(
            CommunityPostId.From(contentId), cancellationToken);

        return post is null ? null : Describe(post);
    }

    public async Task<IReadOnlyDictionary<Guid, ContentDescriptor>> DescribeManyAsync(
        IReadOnlyCollection<Guid> contentIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contentIds);

        if (contentIds.Count == 0)
        {
            return new Dictionary<Guid, ContentDescriptor>();
        }

        var posts = await _posts.GetManyAsync(
            contentIds.Select(CommunityPostId.From).ToList(), cancellationToken);

        return posts.ToDictionary(post => post.Id.Value, Describe);
    }

    private static ContentDescriptor Describe(CommunityPost post) => new()
    {
        Reference = post.Reference,
        AuthorId = post.AuthorId.Value,
        Title = post.Title ?? Summarise(post.Body.Value),
        Exists = !post.IsDeleted && post.Status != PostStatus.Removed,
        Visibility = post.Audience.Level,
        CountryCode = post.Location.CountryCode,
        AreaId = post.Location.AreaId,
        AcceptsComments = !post.CommentsClosed
    };

    /// <summary>
    /// A title for a post that has none.
    /// <para>
    /// The first line, truncated — not a generated summary. A moderation queue row needs enough
    /// text to recognise the post; inventing a description of somebody's words and presenting it
    /// as theirs is not something a queue should do.
    /// </para>
    /// </summary>
    private static string Summarise(string body)
    {
        var firstLine = body.Split('\n')[0].Trim();

        return firstLine.Length <= 120 ? firstLine : firstLine[..117] + "...";
    }
}
