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
/// <b>No catalogue since 14 Oct 2026.</b> The platform is retiring its place catalogue, so a
/// service area is a country, a town by name within a country (<see cref="Town"/>), a circle
/// around a point (<see cref="Radius"/>) or remote. <see cref="Area"/> — one catalogue node by id
/// and path — is legacy: rows already stored keep reading, nothing creates one any more.
/// </para>
/// </summary>
public enum ServiceAreaKind
{
    /// <summary>A whole country, by ISO code.</summary>
    Country = 1,

    /// <summary>
    /// Legacy: one catalogue node and everything beneath it. Not created since 14 Oct 2026; use
    /// <see cref="Town"/> or <see cref="Radius"/>.
    /// </summary>
    Area = 2,

    /// <summary>A circle around a point. The provider's own location unless stated otherwise.</summary>
    Radius = 3,

    /// <summary>No geography. The provider serves anyone who can reach them online.</summary>
    Remote = 4,

    /// <summary>
    /// A town by name within a country — "Mombasa, KE" — matched case-insensitively against the
    /// town a search names (14 Oct 2026).
    /// </summary>
    Town = 5
}
