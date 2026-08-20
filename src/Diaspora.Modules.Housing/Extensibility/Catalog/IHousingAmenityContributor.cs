namespace Diaspora.Modules.Housing.Extensibility.Catalog;

/// <summary>
/// Implemented to supply amenities to the catalogue.
/// <para>
/// <b>More urgently configurable than accommodation types.</b> The amenity that decides a letting
/// is not the same in every market: a generator and a borehole matter in Lagos, air conditioning in
/// Port Louis, double glazing in Manchester. None of those lists belongs in a shared enum, and a
/// deployment that needs one ships a class rather than a migration.
/// </para>
/// </summary>
public interface IHousingAmenityContributor
{
    /// <summary>The amenities this contributor supplies. Called once at startup.</summary>
    IEnumerable<HousingAmenitySeed> GetAmenities();
}
