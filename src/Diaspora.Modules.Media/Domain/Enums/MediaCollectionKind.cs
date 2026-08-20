namespace Diaspora.Modules.Media.Domain.Enums;

/// <summary>
/// What a collection is being used for.
/// <para>
/// Each kind carries its own cardinality rule, taken from the frontend contract report: a
/// provider gallery has exactly four slots because the wizard renders four; a message
/// attachment set caps at five because the picker's <c>selectionLimit</c> is five. Encoding
/// the rule against the kind is what stops each caller inventing its own limit.
/// </para>
/// </summary>
public enum MediaCollectionKind
{
    /// <summary>An ordered set of photos: housing, marketplace, organisation, event.</summary>
    Gallery = 0,

    /// <summary>The provider wizard's four fixed slots.</summary>
    ProviderGallery = 1,

    /// <summary>Files attached to one message.</summary>
    MessageAttachments = 2,

    /// <summary>Evidence attached to a verification or an appeal.</summary>
    VerificationEvidence = 3,

    /// <summary>Images embedded in a guide or an article body.</summary>
    ArticleImages = 4,

    /// <summary>Photos of a place, contributed by members.</summary>
    LocationPhotos = 5,

    /// <summary>Anything else a module needs to group.</summary>
    General = 6
}
