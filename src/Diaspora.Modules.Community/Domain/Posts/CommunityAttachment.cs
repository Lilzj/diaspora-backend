using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Media.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Posts;

/// <summary>
/// One thing hanging off a post or a comment.
/// <para>
/// <b>An attachment stores a media reference and never a URL.</b> That is ADR-005, and it is
/// the rule this class exists to obey: a <see cref="MediaId"/>, a role and a position. Where
/// the bytes live, what they are called, whether a thumbnail exists and what its address is are
/// all Media's business, resolved at render time by asking Media rather than by having stored
/// the answer.
/// </para>
/// <para>
/// The client's <c>imageUrls[]</c> is therefore a projection, not a column. Nothing in this
/// module can produce a storage URL, which is what makes the rule enforceable rather than
/// merely stated.
/// </para>
/// <para>
/// A link attachment carries no media at all, and its preview fields are stored flat rather
/// than as an optional owned value object. That is not a style preference: an optional owned
/// reference whose every property is nullable is the EF Core shape that cannot distinguish an
/// absent instance from a present-but-empty one, and it fails at model-build time. The
/// <see cref="ValueObjects.LinkPreview"/> value object still owns the truncation rules; it is
/// materialised on the way in and out.
/// </para>
/// </summary>
public sealed class CommunityAttachment : Entity<CommunityAttachmentId>
{
    private CommunityAttachment(
        CommunityAttachmentId id,
        AttachmentKind kind,
        MediaId? mediaId,
        string role,
        int position,
        string? caption) : base(id)
    {
        Kind = kind;
        MediaId = mediaId;
        Role = role;
        Position = position;
        Caption = caption;
    }

    private CommunityAttachment() { }

    public AttachmentKind Kind { get; private set; }

    /// <summary>Null only for a <see cref="AttachmentKind.Link"/> attachment.</summary>
    public MediaId? MediaId { get; private set; }

    /// <summary>
    /// A Media reference role, lower-cased. Community uses <c>gallery</c> for post images and
    /// <c>attachment</c> for documents — both from Media's published canonical set, so that
    /// Community does not become the fourth module to invent its own spelling of <c>cover</c>.
    /// </summary>
    public string Role { get; private set; } = MediaReferenceRoles.Gallery;

    public int Position { get; private set; }

    public string? Caption { get; private set; }

    public string? LinkUrl { get; private set; }

    public string? LinkTitle { get; private set; }

    public string? LinkDescription { get; private set; }

    public string? LinkSiteName { get; private set; }

    public Guid? LinkImageMediaId { get; private set; }

    /// <summary>The link preview, materialised from the flat columns. Null for media.</summary>
    public LinkPreview? Preview => LinkUrl is null
        ? null
        : LinkPreview.Unfurled(LinkUrl, LinkTitle, LinkDescription, LinkSiteName, LinkImageMediaId);

    public static CommunityAttachment ForMedia(
        AttachmentKind kind, MediaId mediaId, int position, string? role = null, string? caption = null)
    {
        ArgumentNullException.ThrowIfNull(mediaId);

        return new CommunityAttachment(
            CommunityAttachmentId.New(),
            kind == AttachmentKind.Link ? AttachmentKind.Image : kind,
            mediaId,
            NormaliseRole(role, kind),
            position,
            caption);
    }

    public static CommunityAttachment ForLink(LinkPreview preview, int position)
    {
        ArgumentNullException.ThrowIfNull(preview);

        var attachment = new CommunityAttachment(
            CommunityAttachmentId.New(),
            AttachmentKind.Link,
            null,
            MediaReferenceRoles.Inline,
            position,
            null);

        attachment.ApplyPreview(preview);

        return attachment;
    }

    /// <summary>Replaces the preview. The only path a content enricher takes.</summary>
    public void ApplyPreview(LinkPreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);

        LinkUrl = preview.Url;
        LinkTitle = preview.Title;
        LinkDescription = preview.Description;
        LinkSiteName = preview.SiteName;
        LinkImageMediaId = preview.ImageMediaId;
    }

    internal void MoveTo(int position) => Position = position;

    private static string NormaliseRole(string? role, AttachmentKind kind)
    {
        if (!string.IsNullOrWhiteSpace(role))
        {
            return role.Trim().ToLowerInvariant();
        }

        return kind switch
        {
            AttachmentKind.Document => MediaReferenceRoles.Document,
            AttachmentKind.Link => MediaReferenceRoles.Inline,
            _ => MediaReferenceRoles.Gallery
        };
    }
}
