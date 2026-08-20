using Diaspora.Modules.Location.Domain.Countries;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Extensibility;

namespace Diaspora.Modules.Location.Persistence.Seeding;

/// <summary>
/// Seeds the 195 countries the platform lists.
/// <para>
/// Order zero: every other seeder hangs geography off a country, so the countries must exist
/// first. This is the seeder that replaces the list bundled into the mobile app — a hard-coded
/// array that ships stale, carries no currency, locale or timezone, and cannot say whether the
/// platform actually operates anywhere.
/// </para>
/// </summary>
public sealed class WorldCountrySeeder : ILocationCatalogSeeder
{
    private readonly CountryMetadataCatalog _metadata;

    public WorldCountrySeeder(CountryMetadataCatalog metadata)
    {
        _metadata = metadata;
    }

    public int Order => 0;

    public string Name => "world-countries";

    public async Task SeedAsync(
        ILocationCatalogWriter writer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(writer);

        foreach (var record in WorldCountryData.All)
        {
            var code = CountryCode.TryCreate(record.Code);

            if (code.IsFailure)
            {
                continue;
            }

            var name = PlaceName.TryCreate(record.Name);

            if (name.IsFailure)
            {
                continue;
            }

            // Metadata comes from the contributor catalogue rather than from this table, so a
            // deployment can correct or extend it without editing the shipped list.
            var metadata = _metadata.MetadataFor(code.Value);

            var country = await writer.EnsureCountryAsync(
                code.Value,
                name.Value,
                metadata,
                WorldCountryData.SupportedAtLaunch.Contains(record.Code),
                cancellationToken);

            country.Feature(WorldCountryData.FeaturedAtLaunch.Contains(record.Code));
        }
    }
}
