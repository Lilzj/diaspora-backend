namespace Diaspora.Modules.Marketplace.Domain.Enums;

/// <summary>
/// What a referenced media asset is for.
/// <para>
/// The brief names images, videos, documents, product manuals, galleries and cover images as
/// separate things; they are one reference table with a role, because they differ in how the
/// client renders them and in nothing else. B5 owns the bytes, the thumbnails and the content
/// type — a role here that duplicated any of that would be this module holding Media's data.
/// </para>
/// </summary>
public enum ListingMediaRole
{
    /// <summary>The card image. At most one, and every published listing wants one.</summary>
    Cover = 0,

    /// <summary>The gallery, in order.</summary>
    Gallery = 1,

    Video = 2,

    /// <summary>A receipt, a warranty, a certificate of authenticity.</summary>
    Document = 3,

    /// <summary>The product manual.</summary>
    Manual = 4
}
