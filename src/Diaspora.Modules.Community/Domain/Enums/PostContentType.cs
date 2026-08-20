namespace Diaspora.Modules.Community.Domain.Enums;

/// <summary>
/// What a post is made of.
/// <para>
/// Not the same question as <see cref="CommunityContentType"/>, which asks what a social
/// interaction points at. This asks what the composer produced, so a renderer can choose a
/// layout without inspecting every attachment.
/// </para>
/// <para>
/// A post carrying an image and a link is <see cref="Image"/>, not both: this is the primary
/// presentation, and the attachment list is the full truth. Ordering the check by richness —
/// poll, then video, then image, then document, then link, then text — is a domain rule, and
/// it lives in the aggregate rather than in a renderer so that two clients cannot disagree
/// about what a post is.
/// </para>
/// </summary>
public enum PostContentType
{
    Text = 0,

    Image = 1,

    Video = 2,

    Document = 3,

    Poll = 4,

    Link = 5,

    /// <summary>Structured body a later phase introduces. Rendered as text until then.</summary>
    Rich = 6
}
