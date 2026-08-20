namespace Diaspora.Modules.Marketplace.Domain.Enums;

/// <summary>
/// How worn the item is.
/// <para>
/// Ordered worst-to-best deliberately, so "at least Good" is a comparison rather than a set
/// membership test, and a condition filter is an indexed range rather than an <c>IN</c> over a
/// hand-maintained list.
/// </para>
/// <para>
/// <see cref="NotApplicable"/> exists for the listings where the question is meaningless: a
/// wanted advert, a service, a digital good. Forcing one of the six wear grades onto those would
/// make every such listing claim a condition its seller never chose.
/// </para>
/// </summary>
public enum ProductCondition
{
    /// <summary>The question does not apply — a wanted advert, a service, a digital good.</summary>
    NotApplicable = 0,

    /// <summary>Sold for spares or repair, and said so.</summary>
    ForParts = 1,

    Fair = 2,

    Good = 3,

    Excellent = 4,

    /// <summary>Used briefly, indistinguishable from new.</summary>
    LikeNew = 5,

    /// <summary>Unused, usually sealed.</summary>
    New = 6
}
