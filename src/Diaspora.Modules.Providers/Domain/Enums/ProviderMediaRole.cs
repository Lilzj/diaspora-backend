namespace Diaspora.Modules.Providers.Domain.Enums;

/// <summary>
/// The media roles a provider uses, as constants over B5's open slug vocabulary.
/// <para>
/// Not an enum, and that is deliberate. <c>MediaReference.Role</c> is an open slug precisely so
/// a vertical that needs <c>menu</c> does not need a Media release to get it, and turning it
/// back into a closed enum here would give up the property B5 built. These are the slugs B7
/// uses, published so that a later provider feature and the client agree on spelling — which is
/// the fragmentation B5's own <c>MediaReferenceRoles</c> exists to prevent.
/// </para>
/// <para>
/// The first four are B5's own published names, referenced through
/// <c>MediaReferenceRoles</c> rather than restated, so a change there cannot silently diverge
/// from a copy here. The rest are B7's additions and are exactly the artefacts the brief names:
/// menus, price lists, brochures, certificates.
/// </para>
/// </summary>
public static class ProviderMediaRole
{
    /// <summary>The card and header image. B5's <c>cover</c>.</summary>
    public const string Cover = Media.Domain.ValueObjects.MediaReferenceRoles.Cover;

    /// <summary>The provider's mark. B5's <c>logo</c>.</summary>
    public const string Logo = Media.Domain.ValueObjects.MediaReferenceRoles.Logo;

    /// <summary>The photo gallery. B5's <c>gallery</c>.</summary>
    public const string Gallery = Media.Domain.ValueObjects.MediaReferenceRoles.Gallery;

    /// <summary>A supporting document. B5's <c>document</c>.</summary>
    public const string Document = Media.Domain.ValueObjects.MediaReferenceRoles.Document;

    /// <summary>A food or service menu.</summary>
    public const string Menu = "menu";

    /// <summary>A published price list, where the provider would rather show a PDF than fields.</summary>
    public const string PriceList = "price-list";

    /// <summary>A brochure or leaflet.</summary>
    public const string Brochure = "brochure";

    /// <summary>The evidence behind one <see cref="Providers.ProviderCertification"/>.</summary>
    public const string Certificate = "certificate";

    /// <summary>An introduction or tour video.</summary>
    public const string Video = "video";
}
