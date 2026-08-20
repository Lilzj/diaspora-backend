using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Posts;

/// <summary>
/// A mention inside a post body.
/// <para>
/// Kept on the post as well as in the Mention aggregate, and the split is worth explaining. The
/// aggregate is the notification and audit record — one row per mention, queryable by target,
/// carrying who mentioned whom and when. This is the <em>rendering</em> record: what the body
/// says at which offset, so a renderer can turn text into a link without re-running resolution
/// on every read.
/// </para>
/// </summary>
public sealed class PostMention : Entity<MentionId>
{
    private PostMention(MentionId id, MentionTarget target, int startOffset, int length) : base(id)
    {
        Target = target;
        StartOffset = startOffset;
        Length = length;
    }

    private PostMention() { }

    public MentionTarget Target { get; private set; } = default!;

    /// <summary>Character offset of the handle in the body, including its at-sign.</summary>
    public int StartOffset { get; private set; }

    public int Length { get; private set; }

    internal static PostMention Create(MentionTarget target, int startOffset, int length) =>
        new(MentionId.New(), target, startOffset, length);
}
