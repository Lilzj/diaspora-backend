namespace Diaspora.Modules.Providers.Domain.Enums;

/// <summary>
/// How one service area is described.
/// <para>
/// Four kinds because providers describe their reach in four genuinely different ways, and
/// flattening them loses information a search cannot recover. "I cover the whole of the UK" is
/// a country; "I cover Peckham and Brixton" is two areas; "I travel up to fifteen kilometres
/// from my shop" is a radius; "I work with anyone anywhere" is remote. Storing all four as a
/// radius would require inventing a radius for the first and the last.
/// </para>
/// <para>
/// Every kind resolves against Location's catalogue rather than against B7's own geography.
/// <see cref="Area"/> stores an <c>AreaId</c> and the node's materialised path, so containment
/// at any depth is a prefix match — the same trick B6 uses for neighbourhood feeds, and the
/// reason a provider that serves "London" is correctly returned for a member in Peckham
/// without B7 knowing that Peckham is in London.
/// </para>
/// </summary>
public enum ServiceAreaKind
{
    /// <summary>A whole country, by ISO code.</summary>
    Country = 1,

    /// <summary>One catalogue node and everything beneath it. A city, a borough, a suburb.</summary>
    Area = 2,

    /// <summary>A circle around a point. The provider's own location unless stated otherwise.</summary>
    Radius = 3,

    /// <summary>No geography. The provider serves anyone who can reach them online.</summary>
    Remote = 4
}
