using Diaspora.Modules.Media.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Media.Domain.Collections;

/// <summary>
/// One asset's placement inside a collection.
/// <para>
/// An entity rather than a bare identifier because a placement carries state of its own: where
/// it sits, and what it is captioned in this context. The same photograph can appear in a
/// listing gallery and in a message thread with a different caption in each — a caption on the
/// asset would collapse the two.
/// </para>
/// </summary>
public sealed class MediaCollectionItem : Entity<MediaCollectionItemId>
{
    public const int MaxCaptionLength = 500;

    public MediaCollectionId CollectionId { get; private set; } = default!;

    public MediaId MediaId { get; private set; } = default!;

    /// <summary>Zero-based render order. Contiguous within a collection.</summary>
    public int Position { get; private set; }

    /// <summary>The client's <c>ImageAttachment.caption</c>, scoped to this placement.</summary>
    public string? Caption { get; private set; }

    public DateTimeOffset AddedAt { get; private set; }

    private MediaCollectionItem() { }

    private MediaCollectionItem(
        MediaCollectionItemId id,
        MediaCollectionId collectionId,
        MediaId mediaId,
        int position,
        string? caption) : base(id)
    {
        CollectionId = collectionId;
        MediaId = mediaId;
        Position = position;
        Caption = caption;
        AddedAt = DateTimeOffset.UtcNow;
    }

    internal static MediaCollectionItem Create(
        MediaCollectionId collectionId, MediaId mediaId, int position, string? caption)
    {
        ArgumentNullException.ThrowIfNull(collectionId);
        ArgumentNullException.ThrowIfNull(mediaId);

        return new MediaCollectionItem(
            MediaCollectionItemId.New(),
            collectionId,
            mediaId,
            position,
            Truncate(caption, MaxCaptionLength));
    }

    internal void MoveTo(int position) => Position = position < 0 ? 0 : position;

    internal void Recaption(string? caption) => Caption = Truncate(caption, MaxCaptionLength);

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
