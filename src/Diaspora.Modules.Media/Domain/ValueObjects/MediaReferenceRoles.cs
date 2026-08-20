namespace Diaspora.Modules.Media.Domain.ValueObjects;

/// <summary>
/// The reference roles the frontend contract report found in use.
/// <para>
/// Not a closed set — <see cref="MediaReference"/> accepts any slug, because a vertical that
/// needs <c>menu-photo</c> should not need a Media release to get it. These are the names the
/// existing client already implies, published so that Housing and Marketplace do not invent
/// <c>cover</c>, <c>coverImage</c> and <c>cover_photo</c> independently — which is precisely
/// what the client did across its thirty-one media fields.
/// </para>
/// </summary>
public static class MediaReferenceRoles
{
    /// <summary>Client names unified: <c>avatarUrl</c>, <c>avatar</c>, <c>profilePhoto</c>.</summary>
    public const string Avatar = "avatar";

    /// <summary>Client names unified: <c>coverImage</c>, <c>coverImageUrl</c>, <c>coverPhoto</c>.</summary>
    public const string Cover = "cover";

    public const string Logo = "logo";

    /// <summary>Client names unified: <c>gallery[]</c>, <c>thumbnails[]</c>, <c>imageUrls[]</c>.</summary>
    public const string Gallery = "gallery";

    public const string Attachment = "attachment";

    /// <summary>Client names unified: <c>documentUrl</c>, <c>documentUri</c>.</summary>
    public const string Document = "document";

    /// <summary>The facematch capture in the diaspora verification wizard.</summary>
    public const string Selfie = "selfie";

    public const string Banner = "banner";

    public const string FloorPlan = "floor-plan";

    /// <summary>An image embedded in a guide or article body.</summary>
    public const string Inline = "inline";
}
