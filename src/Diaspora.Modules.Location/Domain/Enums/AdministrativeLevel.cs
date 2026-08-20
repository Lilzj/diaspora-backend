namespace Diaspora.Modules.Location.Domain.Enums;

/// <summary>
/// The rung a geographic node occupies in the administrative hierarchy.
/// <para>
/// The values are ordered and the ordering is load-bearing: a parent's level must be
/// strictly lower than its child's, which is the single invariant that keeps the hierarchy
/// acyclic and traversable without walking it.
/// </para>
/// <para>
/// The gaps between values are deliberate. Real administrative geography is not uniform —
/// England has counties where France has departments and Mauritius has districts with no
/// intervening state at all — so a hierarchy that demanded every level be populated would
/// be wrong everywhere. A node names its own level and its parent; nothing requires the
/// levels to be adjacent. <see cref="Street"/> is declared but not yet issued, so adding
/// street-level geography later is data rather than a schema change.
/// </para>
/// </summary>
public enum AdministrativeLevel
{
    /// <summary>A sovereign state or dependent territory. ISO 3166-1.</summary>
    Country = 10,

    /// <summary>A first-level subdivision: state, province, emirate. ISO 3166-2.</summary>
    StateProvince = 20,

    /// <summary>A grouping used for discovery rather than administration: "Greater London".</summary>
    Region = 30,

    /// <summary>A second-level subdivision: district, county, borough.</summary>
    District = 40,

    /// <summary>A populated place: city, town, village.</summary>
    City = 50,

    /// <summary>A named part of a city: neighbourhood, suburb, ward, area.</summary>
    Neighborhood = 60,

    /// <summary>A postcode or ZIP sector.</summary>
    PostalArea = 70,

    /// <summary>Declared for the future. No node is issued at this level today.</summary>
    Street = 80
}
