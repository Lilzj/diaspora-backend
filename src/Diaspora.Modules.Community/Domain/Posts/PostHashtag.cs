using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Posts;

/// <summary>
/// A hashtag attached to a post.
/// <para>
/// Carries the normalised slug alongside the hashtag identifier, which is redundant on purpose:
/// a hashtag feed filters on the slug, and duplicating one short string here turns that query
/// from a join into an index seek. The display form lives on the Hashtag aggregate, so a
/// member still sees the capitalisation they typed.
/// </para>
/// </summary>
public sealed class PostHashtag : Entity<PostHashtagId>
{
    private PostHashtag(PostHashtagId id, HashtagId hashtagId, string slug, int position) : base(id)
    {
        HashtagId = hashtagId;
        Slug = slug;
        Position = position;
    }

    private PostHashtag() { }

    public HashtagId HashtagId { get; private set; } = default!;

    /// <summary>The normalised slug. Denormalised so a hashtag feed needs no join.</summary>
    public string Slug { get; private set; } = string.Empty;

    /// <summary>Order of appearance in the body.</summary>
    public int Position { get; private set; }

    internal static PostHashtag Create(HashtagId hashtagId, string slug, int position) =>
        new(PostHashtagId.New(), hashtagId, slug, position);
}
