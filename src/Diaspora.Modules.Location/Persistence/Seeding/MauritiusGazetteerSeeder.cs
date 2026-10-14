using Diaspora.Modules.Location.Domain.Areas;
using Diaspora.Modules.Location.Domain.Countries;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Extensibility;

namespace Diaspora.Modules.Location.Persistence.Seeding;

/// <summary>
/// Mauritius's nine districts and its main towns and villages.
/// <para>
/// <b>Added 14 Oct 2026.</b> The launch seed gave Mauritius two districts and five towns, all
/// on the central plateau and in Port Louis. A member in Grand Baie could not choose their town,
/// and a business pinned in Grand Baie was filed under Rose Hill, 28 km away, because Rose Hill
/// was the nearest place the catalogue knew.
/// </para>
/// <para>
/// <b>Runs on every start and adds only what is missing.</b> Unlike the launch seed it does not
/// stop on a non-empty catalogue: <see cref="ILocationCatalogWriter.EnsureAreaAsync"/> returns
/// the existing node for a name under the same parent, so the places already there (and every
/// listing filed under them) are untouched.
/// </para>
/// <para>
/// <b>Names:</b> the English name is written without accents so it can be typed on any keyboard;
/// the French local name keeps them. Search matches either. <b>Centres are approximate</b>
/// (town centres to within about a kilometre) and carry no outline; an OSM boundary import is
/// what would replace them.
/// </para>
/// </summary>
public sealed class MauritiusGazetteerSeeder : ILocationCatalogSeeder
{
    private const int TownProminence = 90;

    private sealed record Place(string Name, string? LocalName, double Latitude, double Longitude);

    private static readonly (Place District, Place[] Towns)[] Districts =
    [
        (new("Plaines Wilhems", null, -20.2833, 57.4833),
        [
            new("Beau Bassin", null, -20.2275, 57.4689),
            new("Vacoas", null, -20.2981, 57.4783),
            new("Phoenix", null, -20.2856, 57.4964),
            new("Floreal", "Floréal", -20.3006, 57.5011),
        ]),
        (new("Port Louis", null, -20.1609, 57.5012), []),
        (new("Pamplemousses", null, -20.1039, 57.5704),
        [
            new("Pamplemousses", null, -20.1039, 57.5704),
            new("Triolet", null, -20.0575, 57.5469),
            new("Trou aux Biches", null, -20.0353, 57.5464),
            new("Mont Choisy", null, -20.0220, 57.5560),
            new("Pointe aux Piments", null, -20.0628, 57.5203),
            new("Arsenal", null, -20.0959, 57.5440),
            new("Terre Rouge", null, -20.1306, 57.5236),
            new("Baie du Tombeau", null, -20.1167, 57.5167),
        ]),
        (new("Riviere du Rempart", "Rivière du Rempart", -20.0560, 57.6550),
        [
            new("Grand Baie", null, -20.0182, 57.5802),
            new("Pereybere", "Pereybère", -20.0055, 57.5917),
            new("Cap Malheureux", null, -19.9842, 57.6136),
            new("Grand Gaube", null, -20.0069, 57.6611),
            new("Goodlands", null, -20.0350, 57.6431),
            new("Poudre d'Or", null, -20.0619, 57.6750),
            new("Riviere du Rempart", "Rivière du Rempart", -20.1019, 57.6822),
        ]),
        (new("Flacq", null, -20.2000, 57.7100),
        [
            new("Centre de Flacq", null, -20.1897, 57.7144),
            new("Poste de Flacq", null, -20.1650, 57.7300),
            new("Belle Mare", null, -20.1950, 57.7672),
            new("Trou d'Eau Douce", null, -20.2400, 57.7820),
            new("Bel Air Riviere Seche", "Bel Air Rivière Sèche", -20.2556, 57.7489),
        ]),
        (new("Moka", null, -20.2300, 57.5750),
        [
            new("Moka", null, -20.2189, 57.4958),
            new("Saint Pierre", null, -20.2178, 57.5208),
            new("Quartier Militaire", null, -20.2483, 57.5856),
        ]),
        (new("Grand Port", null, -20.3850, 57.6400),
        [
            new("Mahebourg", "Mahébourg", -20.4081, 57.7000),
            new("Blue Bay", null, -20.4442, 57.7108),
            new("Plaine Magnien", null, -20.4300, 57.6650),
            new("Rose Belle", null, -20.4000, 57.6000),
        ]),
        (new("Savanne", null, -20.4700, 57.4800),
        [
            new("Souillac", null, -20.5167, 57.5167),
            new("Surinam", null, -20.5094, 57.5050),
            new("Chemin Grenier", null, -20.4869, 57.4656),
            new("Bel Ombre", null, -20.5030, 57.3980),
        ]),
        (new("Black River", "Rivière Noire", -20.3700, 57.3900),
        [
            new("Flic en Flac", null, -20.2743, 57.3711),
            new("Tamarin", null, -20.3256, 57.3706),
            new("Grande Riviere Noire", "Grande Rivière Noire", -20.3600, 57.3667),
            new("La Gaulette", null, -20.4269, 57.3617),
            new("Le Morne", null, -20.4560, 57.3110),
            new("Bambous", null, -20.2567, 57.4061),
            new("Albion", null, -20.2164, 57.4058),
        ]),
    ];

    public int Order => 110;

    public string Name => "mauritius-gazetteer";

    public async Task SeedAsync(ILocationCatalogWriter writer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(writer);

        var country = await writer.EnsureCountryAsync(
            CountryCode.Create("MU"),
            PlaceName.Create("Mauritius"),
            metadata: null,
            isSupported: true,
            cancellationToken);

        foreach (var (district, towns) in Districts)
        {
            var parent = await EnsureAsync(writer, country, null, AdministrativeLevel.District, district, cancellationToken);

            foreach (var town in towns)
            {
                await EnsureAsync(writer, country, parent, AdministrativeLevel.City, town, cancellationToken);
            }
        }
    }

    private static async Task<AdministrativeArea> EnsureAsync(
        ILocationCatalogWriter writer,
        Country country,
        AdministrativeArea? parent,
        AdministrativeLevel level,
        Place place,
        CancellationToken cancellationToken)
    {
        var area = await writer.EnsureAreaAsync(
            country,
            parent,
            level,
            PlaceName.Create(place.Name, place.LocalName),
            GeoCoordinate.Create(place.Latitude, place.Longitude),
            cancellationToken);

        // Only raised, never lowered: a launch node already at 100 keeps its rank.
        if (area.Prominence < TownProminence)
        {
            area.SetProminence(TownProminence);
        }

        return area;
    }
}
