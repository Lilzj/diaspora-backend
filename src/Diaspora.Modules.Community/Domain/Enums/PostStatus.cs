namespace Diaspora.Modules.Community.Domain.Enums;

/// <summary>
/// Where a post is in its lifecycle.
/// <para>
/// The union of the client's two contradictory <c>UpdateStatus</c> declarations —
/// <c>types/update.ts</c> has Active, Expired, Archived, Hidden and Removed;
/// <c>types/creator.ts</c> has Published, Archived, Pinned and Hidden — plus the
/// <see cref="Draft"/> the composer implies but neither declares.
/// </para>
/// <para>
/// <b>Pinned is deliberately not here.</b> A pinned post is a published post that happens to be
/// pinned; making it a status means unpinning has to guess what the status was before, and a
/// pinned-then-expired post has no representable state at all. Pinning is a flag on the
/// aggregate. This is the one place B6 refuses to mirror the client, and the frontend contract
/// report records it.
/// </para>
/// </summary>
public enum PostStatus
{
    /// <summary>Composed but not published. Visible only to its author.</summary>
    Draft = 0,

    /// <summary>Published and inside its lifetime. The client's Active.</summary>
    Published = 1,

    /// <summary>Past its expiry. Still readable by permalink, excluded from feeds by default.</summary>
    Expired = 2,

    /// <summary>Retired by its author. Out of every feed, still theirs.</summary>
    Archived = 3,

    /// <summary>Hidden by moderation. Readable by the author and by staff.</summary>
    Hidden = 4,

    /// <summary>Removed by moderation. Readable by staff only.</summary>
    Removed = 5,

    /// <summary>Deleted by the author. Soft-deleted; the row survives for moderation history.</summary>
    Deleted = 6
}
