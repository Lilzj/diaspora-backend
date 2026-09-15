using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Domain.ValueObjects;

/// <summary>
/// Where the property actually is, and how much of that the platform will say.
/// <para>
/// <b>The address is stored in full and disclosed in part.</b> Those are two different decisions
/// and conflating them is how housing platforms leak. The landlord needs the full address on
/// record — a viewing has to happen somewhere, and a moderator investigating a duplicate listing
/// needs to see that two adverts name one flat. What a browser is shown is a separate question,
/// answered by <see cref="Visibility"/> and enforced by <see cref="Disclose"/>.
/// </para>
/// <para>
/// <b>Nothing outside this type may read the street line.</b> Every mapper, response and search
/// document goes through <see cref="Disclose"/>, which returns a redacted copy rather than a
/// boolean the caller might forget to check. That shape is deliberate: a method that returns the
/// data already filtered cannot be misused by a caller who forgot the rule, and an architecture
/// test asserts no response type exposes the raw properties.
/// </para>
/// <para>
/// <b>The coordinate is fuzzed, not withheld, below full disclosure.</b> A map with no pin is
/// useless to somebody judging a commute, and an exact pin on a public page is a door number in
/// another form. The compromise is a coordinate rounded to roughly a few hundred metres, which
/// puts the marker in the right neighbourhood and nowhere near a specific building. Rounding is
/// applied to the published copy only; the stored value stays exact so a landlord's own map and a
/// distance calculation remain honest.
/// </para>
/// </summary>
public sealed class PropertyAddress : ValueObject
{
    /// <summary>
    /// Decimal places kept on a fuzzed coordinate. Two places is on the order of a kilometre at
    /// the equator: the right part of town, and no particular building on it.
    /// <para>
    /// <b>Public because a search has to honour it too.</b> Fuzzing on the way out protects
    /// nothing if a viewport query may be shrunk around a property until only that property
    /// answers — that is the same coordinate, obtained by bisection instead of by reading it.
    /// A caller filtering on position snaps its rectangle to this grid, so the finest question
    /// anybody can ask is the coarsest answer this class gives.
    /// </para>
    /// </summary>
    public const int FuzzedPrecision = 2;

    private PropertyAddress(
        string? line1,
        string? line2,
        string? postalCode,
        double? latitude,
        double? longitude,
        AddressVisibility visibility)
    {
        Line1 = line1;
        Line2 = line2;
        PostalCode = postalCode;
        Latitude = latitude;
        Longitude = longitude;
        Visibility = visibility;
    }

    private PropertyAddress() { }

    /// <summary>The street line. Never rendered without passing through <see cref="Disclose"/>.</summary>
    public string? Line1 { get; private set; }

    public string? Line2 { get; private set; }

    public string? PostalCode { get; private set; }

    /// <summary>The exact latitude, as stored. The published copy may be fuzzed.</summary>
    public double? Latitude { get; private set; }

    /// <summary>The exact longitude, as stored. The published copy may be fuzzed.</summary>
    public double? Longitude { get; private set; }

    /// <summary>How much of this a viewer is entitled to. Defaults to the safest value.</summary>
    public AddressVisibility Visibility { get; private set; }

    /// <summary>Whether a coordinate was supplied at all.</summary>
    public bool HasCoordinate => Latitude is not null && Longitude is not null;

    /// <summary>
    /// The empty address: no street, no pin, neighbourhood-only disclosure.
    /// <para>
    /// <b>A fresh instance per access, never a cached one.</b> This is an owned type, and a
    /// shared static instance assigned to two listings makes EF treat one as a duplicate of the
    /// other and write nulls into its columns — a defect the platform has carried since B6.
    /// </para>
    /// </summary>
    public static PropertyAddress Undisclosed =>
        new(null, null, null, null, null, AddressVisibility.NeighborhoodOnly);

    public static Result<PropertyAddress> Create(
        string? line1 = null,
        string? line2 = null,
        string? postalCode = null,
        double? latitude = null,
        double? longitude = null,
        AddressVisibility visibility = AddressVisibility.NeighborhoodOnly)
    {
        var first = Clamp(line1, HousingLimits.MaxAddressLine);
        var second = Clamp(line2, HousingLimits.MaxAddressLine);
        var postcode = Clamp(postalCode, HousingLimits.MaxPostalCode);

        if (first is null && !string.IsNullOrWhiteSpace(line1))
        {
            return Result.Failure<PropertyAddress>(HousingErrors.AddressInvalid);
        }

        if (second is null && !string.IsNullOrWhiteSpace(line2))
        {
            return Result.Failure<PropertyAddress>(HousingErrors.AddressInvalid);
        }

        if (postcode is null && !string.IsNullOrWhiteSpace(postalCode))
        {
            return Result.Failure<PropertyAddress>(HousingErrors.AddressInvalid);
        }

        // Both or neither. Half a coordinate is a point on the prime meridian or the equator,
        // which is a pin in the Atlantic rather than a missing value the client can detect.
        if (latitude is null != longitude is null)
        {
            return Result.Failure<PropertyAddress>(HousingErrors.CoordinatesInvalid);
        }

        if (latitude is { } lat && (double.IsNaN(lat) || lat is < -90d or > 90d))
        {
            return Result.Failure<PropertyAddress>(HousingErrors.CoordinatesInvalid);
        }

        if (longitude is { } lon && (double.IsNaN(lon) || lon is < -180d or > 180d))
        {
            return Result.Failure<PropertyAddress>(HousingErrors.CoordinatesInvalid);
        }

        return Result.Success(
            new PropertyAddress(first, second, postcode, latitude, longitude, visibility));
    }

    /// <summary>The same address at a different disclosure level.</summary>
    public PropertyAddress WithVisibility(AddressVisibility visibility) =>
        new(Line1, Line2, PostalCode, Latitude, Longitude, visibility);

    /// <summary>
    /// What a particular viewer is entitled to see, as a redacted copy.
    /// <para>
    /// <b>The one supported way to read an address.</b> Returning a filtered value rather than a
    /// permission flag is what makes the rule unforgettable: there is no path by which a caller
    /// obtains the street line without having stated who is asking.
    /// </para>
    /// </summary>
    /// <param name="isOwner">The landlord, who always sees their own property in full.</param>
    /// <param name="isStaff">A moderator, who needs the full address to judge a duplicate.</param>
    /// <param name="isAuthenticated">Whether anybody is signed in at all.</param>
    /// <param name="hasConfirmedViewing">
    /// Whether this viewer has a scheduled or completed viewing. The condition
    /// <see cref="AddressVisibility.OnViewingConfirmed"/> turns on, and the reason booking a
    /// viewing is the intended path to an address rather than a workaround for one.
    /// </param>
    public PropertyAddress Disclose(
        bool isOwner,
        bool isStaff,
        bool isAuthenticated,
        bool hasConfirmedViewing)
    {
        if (isOwner || isStaff)
        {
            return this;
        }

        return Visibility switch
        {
            AddressVisibility.Public => this,

            AddressVisibility.AuthenticatedMembers when isAuthenticated => this,

            AddressVisibility.OnViewingConfirmed when hasConfirmedViewing => this,

            // The street without a number: enough to judge a commute, not enough to knock. The
            // second line is dropped with it, because "Flat 4" is a door.
            AddressVisibility.StreetOnly => new PropertyAddress(
                StripBuildingNumber(Line1),
                null,
                PostalCode,
                Fuzz(Latitude),
                Fuzz(Longitude),
                Visibility),

            _ => new PropertyAddress(
                null, null, null, Fuzz(Latitude), Fuzz(Longitude), Visibility)
        };
    }

    /// <summary>
    /// The coordinate a stranger sees for a stored position — exact only for a public address,
    /// fuzzed to <see cref="FuzzedPrecision"/> otherwise.
    /// <para>
    /// For a caller holding the columns rather than the value object: "nearest first" sorts
    /// candidates it reads as bare coordinates. It runs <see cref="Disclose"/> itself, so there is
    /// one rule for what a distance may be measured from, and a sort cannot learn more than a
    /// search result is shown.
    /// </para>
    /// </summary>
    public static (double? Latitude, double? Longitude) PublicCoordinates(
        double? latitude, double? longitude, AddressVisibility visibility)
    {
        var published = new PropertyAddress(null, null, null, latitude, longitude, visibility)
            .Disclose(isOwner: false, isStaff: false, isAuthenticated: false, hasConfirmedViewing: false);

        return (published.Latitude, published.Longitude);
    }

    /// <summary>
    /// Drops a leading building number or flat identifier from a street line.
    /// <para>
    /// Deliberately conservative: it removes a leading run of digits and separators and nothing
    /// else. A cleverer parser would sometimes strip part of a street name — "12th Avenue" is a
    /// street, "12 Avenue Road" is a door — and on this field an over-eager redaction is merely
    /// unhelpful while an under-eager one publishes an address.
    /// </para>
    /// </summary>
    private static string? StripBuildingNumber(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        var span = line.AsSpan().TrimStart();
        var index = 0;

        while (index < span.Length
               && (char.IsAsciiDigit(span[index]) || span[index] is '-' or '/' or ','))
        {
            index++;
        }

        // Nothing numeric at the front: the line is already a street name.
        if (index == 0)
        {
            return line.Trim();
        }

        var remainder = span[index..].TrimStart();

        // The whole line was a number. Better to say nothing than to publish a bare door number.
        return remainder.IsEmpty ? null : remainder.ToString();
    }

    private static double? Fuzz(double? value) =>
        value is { } exact ? Math.Round(exact, FuzzedPrecision) : null;

    private static string? Clamp(string? value, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length > maximum ? null : trimmed;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Line1;
        yield return Line2;
        yield return PostalCode;
        yield return Latitude;
        yield return Longitude;
        yield return Visibility;
    }
}
