namespace Diaspora.Modules.Community.Domain.Enums;

/// <summary>
/// What kind of thing an attachment is, from the perspective of the content carrying it.
/// <para>
/// Deliberately <b>not</b> the Media module MediaKind. B6 stores a media reference and knows
/// what it asked for; asking Media what it turned out to be is a read of another module state
/// on a render path. The two agreeing is a projection concern, not a storage one, and a post
/// that says image while Media says the asset is a video is a data question worth being able
/// to ask.
/// </para>
/// <para>
/// <see cref="Link"/> carries no media at all. It is here because a link preview is an
/// attachment to every renderer that draws one, and modelling it as a separate collection
/// would mean two ordering schemes for one visual row.
/// </para>
/// </summary>
public enum AttachmentKind
{
    Image = 0,

    Video = 1,

    Document = 2,

    Audio = 3,

    /// <summary>A URL with optional preview metadata. Carries no media reference.</summary>
    Link = 4
}
