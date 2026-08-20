namespace Diaspora.Modules.Media.Domain.Enums;

/// <summary>
/// The kind of thing an asset was uploaded for.
/// <para>
/// Deliberately an enum with a reserved <see cref="Other"/> rather than a free-form string.
/// A string would let any module invent an owner type, and the value would then appear in
/// indexes, audit records and access rules with no way to enumerate what exists. Adding a
/// vertical adds a value here — an additive change, and the only line B5 expects a future
/// phase to touch.
/// </para>
/// <para>
/// The list is the roadmap: every module named in the B5 brief as a future consumer already
/// has its value, so Community, Housing, Jobs and the rest reference media on the day they
/// ship without a Media change.
/// </para>
/// </summary>
public enum MediaOwnerType
{
    /// <summary>Not attached to anything yet — an upload that has not been claimed.</summary>
    Unassigned = 0,

    /// <summary>A member's own profile: avatar, cover.</summary>
    UserProfile = 1,

    /// <summary>A Trust verification request or appeal.</summary>
    Verification = 2,

    Community = 3,
    Provider = 4,
    Guide = 5,
    CommunityUpdate = 6,
    Job = 7,
    HousingListing = 8,
    MarketplaceListing = 9,
    Event = 10,
    Organization = 11,
    Message = 12,
    Location = 13,

    /// <summary>Reserved for the AI module's analysis inputs.</summary>
    AiRequest = 14,

    /// <summary>
    /// A comment's single attachment.
    /// <para>
    /// <b>Added in PSR-1, and the omission was a live defect rather than a gap.</b> B6's
    /// <c>Comment</c> has carried an <c>AttachmentMediaId</c> since it shipped, and the handler
    /// that set it had no owner type to claim the asset under — so a member who attached a
    /// photograph to a comment watched it vanish a day later, when the orphan sweeper collected
    /// an asset nothing had claimed. Every upload succeeded, which is why nothing reported it.
    /// </para>
    /// <para>
    /// <see cref="CommunityUpdate"/> was not reusable for it: the two are swept, counted and
    /// access-checked as one owner would be, and a comment deleted with its post would take the
    /// post's images with it.
    /// </para>
    /// </summary>
    Comment = 15,

    /// <summary>An owner type a later module introduced before this enum caught up.</summary>
    Other = 99
}
