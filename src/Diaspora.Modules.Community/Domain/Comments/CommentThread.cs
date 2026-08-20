using Diaspora.Modules.Community.Domain.Policies;
using Diaspora.Modules.Community.Domain.ValueObjects;

namespace Diaspora.Modules.Community.Domain.Comments;

/// <summary>
/// A flat set of comments, assembled into the tree a client renders.
/// <para>
/// The one place the module turns rows into a thread. Three rules live here and nowhere else,
/// which is the point: a renderer that reimplemented any of them would produce a thread that
/// disagreed with every other surface.
/// </para>
/// <para>
/// <b>1. Pinned comments float.</b> A pinned comment sorts first among its siblings whatever its
/// timestamp, because pinning it is what somebody did to make it first.
/// </para>
/// <para>
/// <b>2. Beyond the rendered depth, replies flatten.</b> The true parent is preserved on the
/// row; only the drawn position changes. Past about six levels a mobile thread is a column of
/// single words.
/// </para>
/// <para>
/// <b>3. Orphans are adopted, not dropped.</b> A reply whose parent is not in the supplied set —
/// because the page cut off above it, or because the parent was hard-deleted by a data
/// correction — attaches at the root rather than disappearing. A comment that exists and is
/// never drawn is the bug nobody can reproduce.
/// </para>
/// </summary>
public sealed class CommentThread
{
    private CommentThread(ContentReference content, IReadOnlyList<CommentNode> roots, int total)
    {
        Content = content;
        Roots = roots;
        TotalCount = total;
    }

    public ContentReference Content { get; }

    public IReadOnlyList<CommentNode> Roots { get; }

    public int TotalCount { get; }

    public static CommentThread Assemble(
        ContentReference content,
        IReadOnlyCollection<Comment> comments,
        int maxRenderedDepth = CommunityLimits.MaxRenderedCommentDepth)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(comments);

        var ordered = comments
            .OrderByDescending(comment => comment.IsPinned)
            .ThenBy(comment => comment.CreatedAt)
            .ToList();

        var nodes = new Dictionary<Guid, CommentNode>();
        var roots = new List<CommentNode>();

        // One pass in path order guarantees a parent is placed before any of its children,
        // because a child path is its parent path plus one segment and therefore sorts after it.
        foreach (var comment in ordered.OrderBy(comment => comment.Depth))
        {
            var parentNode = comment.ParentCommentId is { } parentId
                ? nodes.GetValueOrDefault(parentId.Value)
                : null;

            var renderedDepth = parentNode is null
                ? 0
                : Math.Min(parentNode.RenderedDepth + 1, maxRenderedDepth);

            var node = new CommentNode(comment, renderedDepth);
            nodes[comment.Id.Value] = node;

            if (parentNode is null)
            {
                roots.Add(node);
            }
            else
            {
                parentNode.Add(node);
            }
        }

        // Restore sibling order within each parent, which the depth-ordered pass disturbed.
        roots.Sort(Compare);

        foreach (var root in roots)
        {
            root.SortReplies(Compare);
        }

        return new CommentThread(content, roots, comments.Count);
    }

    private static int Compare(CommentNode left, CommentNode right)
    {
        if (left.Comment.IsPinned != right.Comment.IsPinned)
        {
            return left.Comment.IsPinned ? -1 : 1;
        }

        return left.Comment.CreatedAt.CompareTo(right.Comment.CreatedAt);
    }
}
