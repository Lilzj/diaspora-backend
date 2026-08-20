namespace Diaspora.Modules.Community.Domain.Comments;

/// <summary>
/// One comment and its replies, as a tree.
/// <para>
/// A projection type, not persisted. It exists so the assembly of a thread happens once, in the
/// domain, rather than in whichever handler happens to need it — which is how two endpoints end
/// up disagreeing about what "collapsed" means.
/// </para>
/// </summary>
public sealed class CommentNode
{
    private readonly List<CommentNode> _replies = [];

    internal CommentNode(Comment comment, int renderedDepth)
    {
        Comment = comment;
        RenderedDepth = renderedDepth;
    }

    public Comment Comment { get; }

    /// <summary>
    /// The depth this node is drawn at, which is not always its true depth. Past the rendered
    /// ceiling, replies are flattened into the deepest visible ancestor — the true depth is
    /// still on the comment, so a later client with more room re-renders the same rows properly.
    /// </summary>
    public int RenderedDepth { get; }

    public IReadOnlyList<CommentNode> Replies => _replies.AsReadOnly();

    /// <summary>Every descendant, not just direct replies. What a collapsed node hides.</summary>
    public int TotalDescendants =>
        _replies.Count + _replies.Sum(reply => reply.TotalDescendants);

    internal void Add(CommentNode reply) => _replies.Add(reply);

    /// <summary>
    /// Sorts this node's replies and, recursively, theirs.
    /// <para>
    /// Internal and mutating rather than returning a sorted copy, because the tree is assembled
    /// in one pass and ordered in a second. <see cref="Replies"/> hands out a read-only view, so
    /// this is the only way the order can be set — which is what stops a caller sorting one
    /// branch by a different rule.
    /// </para>
    /// </summary>
    internal void SortReplies(Comparison<CommentNode> comparison)
    {
        _replies.Sort(comparison);

        foreach (var reply in _replies)
        {
            reply.SortReplies(comparison);
        }
    }
}
