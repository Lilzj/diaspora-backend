using Diaspora.Modules.Community.Domain.Comments;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Extensibility.Content;

namespace Diaspora.Modules.Community.Infrastructure.Content;

/// <summary>
/// Community's content source for comments.
/// <para>
/// Comments are reportable and reactable but <b>not commentable</b> — a reply is a comment on
/// the same content with a parent, not a comment on a comment. <see cref="ContentDescriptor.AcceptsComments"/>
/// is therefore always false here, which is what stops a client accidentally creating a second
/// threading model.
/// </para>
/// </summary>
public sealed class CommentContentSource : ICommunityContentSource
{
    private readonly ICommentRepository _comments;

    public CommentContentSource(ICommentRepository comments) => _comments = comments;

    public CommunityContentType ContentType => CommunityContentType.Comment;

    public async Task<ContentDescriptor?> DescribeAsync(
        Guid contentId, CancellationToken cancellationToken = default)
    {
        var comment = await _comments.GetAsync(CommentId.From(contentId), cancellationToken);

        return comment is null ? null : Describe(comment);
    }

    public async Task<IReadOnlyDictionary<Guid, ContentDescriptor>> DescribeManyAsync(
        IReadOnlyCollection<Guid> contentIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contentIds);

        var described = new Dictionary<Guid, ContentDescriptor>();

        foreach (var id in contentIds.Distinct())
        {
            var comment = await _comments.GetAsync(CommentId.From(id), cancellationToken);

            if (comment is not null)
            {
                described[id] = Describe(comment);
            }
        }

        return described;
    }

    private static ContentDescriptor Describe(Comment comment) => new()
    {
        Reference = comment.Reference,
        AuthorId = comment.AuthorId.Value,
        Title = comment.IsTombstoned ? "(removed comment)" : Summarise(comment.Body.Value),
        Exists = !comment.IsDeleted,
        Visibility = VisibilityLevel.Public,
        AcceptsComments = false
    };

    private static string Summarise(string body) =>
        body.Length <= 120 ? body : body[..117] + "...";
}
