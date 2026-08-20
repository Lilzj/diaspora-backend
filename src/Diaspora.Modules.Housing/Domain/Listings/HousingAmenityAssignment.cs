using Diaspora.Modules.Housing.Domain.Policies;

namespace Diaspora.Modules.Housing.Domain.Listings;

/// <summary>
/// One amenity a property has, by catalogue key.
/// <para>
/// <b>A key rather than a foreign key to the catalogue row, and that is deliberate.</b> Amenities
/// are configurable: a deployment adds "Borehole" or retires "Fax Line", and a listing holding a
/// row identifier would break when the catalogue was reseeded from a contributor. Holding the key
/// means a retired amenity still renders on the listings that claimed it, which is the honest
/// outcome - the property still has the thing.
/// </para>
/// <para>
/// <b><see cref="Note"/> is what stops the amenity list being a lie.</b> "Generator" is true of a
/// building with a generator that runs four hours a night, and true of one with a whole-house
/// inverter. A member arriving from abroad cannot tell those apart from a tick, and a short note
/// beside the tick is the difference.
/// </para>
/// </summary>
public sealed class HousingAmenityAssignment
{
    private HousingAmenityAssignment(string amenityKey, string? note)
    {
        AmenityKey = amenityKey;
        Note = note;
    }

    private HousingAmenityAssignment() { }

    /// <summary>The catalogue key, normalised. See the class summary for why it is not an id.</summary>
    public string AmenityKey { get; private set; } = string.Empty;

    /// <summary>The landlord's qualification of the tick. See the class summary.</summary>
    public string? Note { get; private set; }

    public static HousingAmenityAssignment Create(string amenityKey, string? note = null)
    {
        var trimmed = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        if (trimmed is { Length: > HousingLimits.MaxDisplayName })
        {
            trimmed = trimmed[..HousingLimits.MaxDisplayName];
        }

        return new HousingAmenityAssignment(NormalizeKey(amenityKey) ?? string.Empty, trimmed);
    }

    /// <summary>
    /// The comparable form of an amenity key. Shared with the catalogue so a contributor
    /// supplying "Wi-Fi" and a listing supplying "wi-fi" land on one amenity rather than two.
    /// </summary>
    public static string? NormalizeKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var normalized = key.Trim().ToLowerInvariant().Replace(' ', '-');

        return normalized.Length > HousingLimits.MaxAmenityKey ? null : normalized;
    }
}
