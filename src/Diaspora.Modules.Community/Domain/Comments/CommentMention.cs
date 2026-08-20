using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Comments;

/// <summary>
/// A mention inside a comment body.
/// <para>
/// A separate type from the post's mention rather than a shared one, because the two are owned
/// by different aggregates and therefore live in different tables. Sharing the CLR type would
/// mean one owned-collection configuration serving two owners, which EF supports and which
/// makes the two tables impossible to index differently — and a comment mention is queried by
/// comment while a post mention is queried by post.
/// </para>
/// </summary>
public sealed class CommentMention : Entity<MentionId>
{
    private CommentMention(MentionId id, MentionTarget target, int startOffset, int length)
        : base(id)
    {
        Target = target;
        StartOffset = startOffset;
        Length = length;
    }

    private CommentMention() { }

    public MentionTarget Target { get; private set; } = default!;

    public int StartOffset { get; private set; }

    public int Length { get; private set; }

    internal static CommentMention Create(MentionTarget target, int startOffset, int length) =>
        new(MentionId.New(), target, startOffset, length);
}
