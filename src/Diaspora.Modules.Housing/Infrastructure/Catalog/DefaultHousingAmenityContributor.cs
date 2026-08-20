using Diaspora.Modules.Housing.Extensibility.Catalog;

namespace Diaspora.Modules.Housing.Infrastructure.Catalog;

/// <summary>
/// The amenities the platform ships with.
/// <para>
/// <b>Every amenity the B11 brief names, grouped so a listing form is not a wall of forty
/// ticks.</b> The list is deliberately international rather than European: a generator and a solar
/// installation matter as much in Lagos as double glazing does in Manchester, and a deployment that
/// needs a borehole ships a contributor rather than waiting for this file to change.
/// </para>
/// <para>
/// <b>The three accessibility entries are flagged rather than grouped.</b> Step-free access is not
/// a convenience to be listed beside a swimming pool: for the members who need it, it decides
/// whether a property is habitable at all, and the flag is what lets that filter work regardless of
/// how a deployment chose to group things.
/// </para>
/// </summary>
public sealed class DefaultHousingAmenityContributor : IHousingAmenityContributor
{
    private const string Essentials = "Essentials";
    private const string Utilities = "Utilities";
    private const string Comfort = "Comfort";
    private const string Outdoor = "Outdoor";
    private const string Building = "Building";
    private const string Safety = "Safety";
    private const string Transport = "Transport";
    private const string Accessibility = "Accessibility";

    public IEnumerable<HousingAmenitySeed> GetAmenities()
    {
        // ── Essentials ──────────────────────────────────────────────────────────────────────
        yield return Amenity("wifi", "Wi-Fi", Essentials, 10);
        yield return Amenity("kitchen", "Kitchen", Essentials, 20);
        yield return Amenity("laundry", "Laundry", Essentials, 30);
        yield return Amenity("workspace", "Workspace", Essentials, 40);

        // ── Utilities. The group that decides a letting outside western Europe. ─────────────
        yield return Amenity("water-heater", "Water Heater", Utilities, 50);
        yield return Amenity("generator", "Generator", Utilities, 60);
        yield return Amenity("solar", "Solar Power", Utilities, 70);

        // ── Comfort ─────────────────────────────────────────────────────────────────────────
        yield return Amenity("air-conditioning", "Air Conditioning", Comfort, 80);
        yield return Amenity("gym", "Gym", Comfort, 90);
        yield return Amenity("swimming-pool", "Swimming Pool", Comfort, 100);

        // ── Outdoor ─────────────────────────────────────────────────────────────────────────
        yield return Amenity("balcony", "Balcony", Outdoor, 110);
        yield return Amenity("garden", "Garden", Outdoor, 120);

        // ── Building ────────────────────────────────────────────────────────────────────────
        yield return Amenity("elevator", "Lift", Building, 130);
        yield return Amenity("parking", "Parking", Building, 140);

        // ── Safety ──────────────────────────────────────────────────────────────────────────
        yield return Amenity("security", "Security", Safety, 150);
        yield return Amenity("cctv", "CCTV", Safety, 160);

        // ── Transport ───────────────────────────────────────────────────────────────────────
        yield return Amenity("nearby-bus-stop", "Near a Bus Stop", Transport, 170);
        yield return Amenity("nearby-train", "Near a Train Station", Transport, 180);

        // ── Accessibility. Flagged, not merely grouped. See the class summary. ──────────────
        yield return new HousingAmenitySeed
        {
            Key = "wheelchair-access",
            Name = "Wheelchair Access",
            Group = Accessibility,
            IsAccessibilityFeature = true,
            Order = 190
        };

        yield return new HousingAmenitySeed
        {
            Key = "step-free-entrance",
            Name = "Step-Free Entrance",
            Group = Accessibility,
            IsAccessibilityFeature = true,
            Order = 200
        };

        yield return new HousingAmenitySeed
        {
            Key = "accessible-bathroom",
            Name = "Accessible Bathroom",
            Group = Accessibility,
            IsAccessibilityFeature = true,
            Order = 210
        };
    }

    private static HousingAmenitySeed Amenity(
        string key, string name, string group, int order) => new()
    {
        Key = key,
        Name = name,
        Group = group,
        Order = order
    };
}
