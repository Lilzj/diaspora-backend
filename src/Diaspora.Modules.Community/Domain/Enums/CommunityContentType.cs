namespace Diaspora.Modules.Community.Domain.Enums;

/// <summary>
/// The kind of thing a social interaction points at.
/// <para>
/// <b>This is the single most important type in the module.</b> Comments, reactions, reports,
/// bookmarks, shares and mentions all address content through
/// <see cref="ValueObjects.ContentReference"/>, which is this enum plus an identifier. That
/// indirection is what makes B6 reusable infrastructure rather than a post module with social
/// features bolted on: Guides (B8) becomes commentable by registering a content source, not by
/// growing its own comment table.
/// </para>
/// <para>
/// The client already thinks this way. CommentContentType, ReactionContentType,
/// ModerationContentType and SavedContentType are four overlapping declarations of the same
/// idea across four files in its types folder. This is their union.
/// </para>
/// <para>
/// An enum with a reserved <see cref="Other"/> rather than a free-form string, for the reason
/// B5 gives for its own owner type: a string lets any module invent a value that then appears
/// in indexes, moderation queues and audit records with no way to enumerate what exists.
/// Adding a vertical adds a member here, which is additive, and is the only line B6 expects a
/// later phase to touch.
/// </para>
/// </summary>
public enum CommunityContentType
{
    /// <summary>A community post. The client calls this a CommunityUpdate.</summary>
    Post = 1,

    /// <summary>A comment. Comments are themselves reactable, reportable and moderatable.</summary>
    Comment = 2,

    /// <summary>A long-form guide. Owned by B8; commentable through this module.</summary>
    Guide = 3,

    /// <summary>A service provider listing.</summary>
    Listing = 4,

    Job = 5,

    Housing = 6,

    Marketplace = 7,

    Event = 8,

    Organization = 9,

    /// <summary>A poll, addressable independently of the post that carries it.</summary>
    Poll = 10,

    /// <summary>A member profile. Followable and reportable, not commentable.</summary>
    Member = 11,

    /// <summary>A content type a later module introduced before this enum caught up.</summary>
    Other = 99
}
