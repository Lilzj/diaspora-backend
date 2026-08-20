using Diaspora.Modules.Location.Domain.Areas;
using Diaspora.Modules.Location.Domain.Countries;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Extensibility;

namespace Diaspora.Modules.Location.Persistence.Seeding;

/// <summary>
/// Seeds the geography the product already assumes exists.
/// <para>
/// Every node here is transcribed from the frontend's own <c>mockLocationDatabase</c>, with
/// the same names and the same coordinates. That is the point: the app's default location is
/// Peckham, its search fixtures are these fifteen places, and its neighbourhood hub links
/// resolve these slugs. A backend that seeded a different fifteen would leave every one of
/// those broken while appearing to work.
/// </para>
/// <para>
/// Fifteen nodes is not a gazetteer, and it is not pretending to be one — see known gap #3.
/// A real dataset arrives through another <see cref="ILocationCatalogSeeder"/>, and this
/// seeder is idempotent so the two coexist without conflict.
/// </para>
/// </summary>
public sealed class LaunchGeographySeeder : ILocationCatalogSeeder
{
    /// <summary>Ranks the launch cities above whatever a later import adds beside them.</summary>
    private const int LaunchProminence = 100;

    public int Order => 100;

    public string Name => "launch-geography";

    public async Task SeedAsync(
        ILocationCatalogWriter writer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(writer);

        // Only on an empty catalogue. Once a real gazetteer has been imported, re-adding
        // fifteen fixture nodes beside it would create duplicates that outrank the real ones
        // on prominence.
        if (await writer.HasAreasAsync(cancellationToken))
        {
            return;
        }

        await SeedUnitedKingdomAsync(writer, cancellationToken);
        await SeedMauritiusAsync(writer, cancellationToken);
    }

    private static async Task SeedUnitedKingdomAsync(
        ILocationCatalogWriter writer, CancellationToken cancellationToken)
    {
        var country = await EnsureCountryAsync(writer, "GB", "United Kingdom", cancellationToken);

        var greaterLondon = await AreaAsync(
            writer, country, null, AdministrativeLevel.Region, "Greater London",
            51.5074, -0.1278, cancellationToken);

        var london = await AreaAsync(
            writer, country, greaterLondon, AdministrativeLevel.City, "London",
            51.5074, -0.1278, cancellationToken);

        // The eight London neighbourhoods the frontend's fixtures name, at its coordinates.
        await AreaAsync(writer, country, london, AdministrativeLevel.Neighborhood,
            "Peckham", 51.4742, -0.0691, cancellationToken);
        await AreaAsync(writer, country, london, AdministrativeLevel.Neighborhood,
            "Brixton", 51.4624, -0.1149, cancellationToken);
        await AreaAsync(writer, country, london, AdministrativeLevel.Neighborhood,
            "Lewisham", 51.4627, -0.0101, cancellationToken);
        await AreaAsync(writer, country, london, AdministrativeLevel.Neighborhood,
            "Woolwich", 51.4933, 0.0632, cancellationToken);
        await AreaAsync(writer, country, london, AdministrativeLevel.Neighborhood,
            "Hackney", 51.5450, -0.0553, cancellationToken);
        await AreaAsync(writer, country, london, AdministrativeLevel.Neighborhood,
            "Croydon", 51.3762, -0.0982, cancellationToken);
        await AreaAsync(writer, country, london, AdministrativeLevel.Neighborhood,
            "Greenwich", 51.4802, 0.0030, cancellationToken);
        await AreaAsync(writer, country, london, AdministrativeLevel.Neighborhood,
            "Tottenham", 51.5900, -0.0700, cancellationToken);

        var greaterManchester = await AreaAsync(
            writer, country, null, AdministrativeLevel.Region, "Greater Manchester",
            53.4808, -2.2426, cancellationToken);

        var manchester = await AreaAsync(
            writer, country, greaterManchester, AdministrativeLevel.City, "Manchester",
            53.4808, -2.2426, cancellationToken);

        await AreaAsync(writer, country, manchester, AdministrativeLevel.Neighborhood,
            "Salford", 53.4833, -2.2589, cancellationToken);
        await AreaAsync(writer, country, manchester, AdministrativeLevel.Neighborhood,
            "Fallowfield", 53.4442, -2.2223, cancellationToken);
    }

    private static async Task SeedMauritiusAsync(
        ILocationCatalogWriter writer, CancellationToken cancellationToken)
    {
        var country = await EnsureCountryAsync(writer, "MU", "Mauritius", cancellationToken);

        // Mauritius organises by district with no state above it — the case that makes an
        // eight-level hierarchy with skippable rungs necessary rather than merely tidy.
        var plainesWilhems = await AreaAsync(
            writer, country, null, AdministrativeLevel.District, "Plaines Wilhems",
            -20.2833, 57.4833, cancellationToken);

        var roseHill = await AreaAsync(
            writer, country, plainesWilhems, AdministrativeLevel.City, "Rose Hill",
            -20.2417, 57.4806, cancellationToken);

        await AreaAsync(writer, country, roseHill, AdministrativeLevel.Neighborhood,
            "Rose Hill Central", -20.2417, 57.4806, cancellationToken);

        var quatreBornes = await AreaAsync(
            writer, country, plainesWilhems, AdministrativeLevel.City, "Quatre Bornes",
            -20.2644, 57.4786, cancellationToken);

        await AreaAsync(writer, country, quatreBornes, AdministrativeLevel.Neighborhood,
            "Quatre Bornes Center", -20.2644, 57.4786, cancellationToken);

        var curepipe = await AreaAsync(
            writer, country, plainesWilhems, AdministrativeLevel.City, "Curepipe",
            -20.3167, 57.5258, cancellationToken);

        await AreaAsync(writer, country, curepipe, AdministrativeLevel.Neighborhood,
            "Curepipe Town", -20.3167, 57.5258, cancellationToken);

        var portLouisDistrict = await AreaAsync(
            writer, country, null, AdministrativeLevel.District, "Port Louis",
            -20.1609, 57.5012, cancellationToken);

        // A city and its district share a name here, which is why slugs are unique per parent
        // rather than globally: "port-louis" is legitimately two nodes.
        var portLouis = await AreaAsync(
            writer, country, portLouisDistrict, AdministrativeLevel.City, "Port Louis",
            -20.1609, 57.5012, cancellationToken);

        await AreaAsync(writer, country, portLouis, AdministrativeLevel.Neighborhood,
            "Waterfront", -20.1608, 57.5012, cancellationToken);
        await AreaAsync(writer, country, portLouis, AdministrativeLevel.Neighborhood,
            "Ward IV", -20.1642, 57.5029, cancellationToken);
    }

    private static async Task<Country> EnsureCountryAsync(
        ILocationCatalogWriter writer, string code, string name, CancellationToken cancellationToken) =>
        await writer.EnsureCountryAsync(
            CountryCode.Create(code),
            PlaceName.Create(name),
            metadata: null,
            isSupported: true,
            cancellationToken);

    private static async Task<AdministrativeArea> AreaAsync(
        ILocationCatalogWriter writer,
        Country country,
        AdministrativeArea? parent,
        AdministrativeLevel level,
        string name,
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        var area = await writer.EnsureAreaAsync(
            country,
            parent,
            level,
            PlaceName.Create(name),
            GeoCoordinate.Create(latitude, longitude),
            cancellationToken);

        area.SetProminence(LaunchProminence);

        return area;
    }
}
