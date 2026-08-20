using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Domain.ValueObjects;

/// <summary>
/// Where an event physically is.
/// <para>
/// <b>A landmark is a first-class field, and that is the most locally-informed decision in the
/// module.</b> Across much of the diaspora this platform serves, "opposite the Total filling
/// station, Awolowo Road" is how people actually find somewhere, and a schema with only a street
/// address forces that into a line that mapping software then mangles. It is stored separately,
/// rendered separately, and never sent to a geocoder.
/// </para>
/// <para>
/// <b>The address is published in full, unlike B11's.</b> A housing listing hides its address until
/// a viewing is arranged, because the address is the asset. An event's address is the invitation —
/// hiding it would defeat the purpose — so the privacy work here is on the attendee list instead.
/// </para>
/// </summary>
public sealed class EventVenue : ValueObject
{
    private EventVenue(
        string? name,
        string? addressLine1,
        string? addressLine2,
        string? landmark,
        string? postalCode,
        double? latitude,
        double? longitude,
        string? onlineUrl)
    {
        Name = name;
        AddressLine1 = addressLine1;
        AddressLine2 = addressLine2;
        Landmark = landmark;
        PostalCode = postalCode;
        Latitude = latitude;
        Longitude = longitude;
        OnlineUrl = onlineUrl;
    }

    private EventVenue() { }

    /// <summary>An event with nowhere yet. What a draft starts with.</summary>
    public static EventVenue Unset => new(null, null, null, null, null, null, null, null);

    /// <summary>What the place is called — the hall, the church, the community centre.</summary>
    public string? Name { get; private set; }

    public string? AddressLine1 { get; private set; }

    public string? AddressLine2 { get; private set; }

    /// <summary>How people will actually find it. Never geocoded. See the class summary.</summary>
    public string? Landmark { get; private set; }

    public string? PostalCode { get; private set; }

    public double? Latitude { get; private set; }

    public double? Longitude { get; private set; }

    /// <summary>
    /// Where an online or hybrid event happens. Held here so the venue is one value object rather
    /// than two, and validated as an http(s) URL for the same reason the social links are.
    /// </summary>
    public string? OnlineUrl { get; private set; }

    public bool HasCoordinate => Latitude is not null && Longitude is not null;

    /// <summary>Whether the venue says enough for somebody to turn up at it.</summary>
    public bool IsAddressable =>
        !string.IsNullOrWhiteSpace(Name)
        || !string.IsNullOrWhiteSpace(AddressLine1)
        || !string.IsNullOrWhiteSpace(Landmark);

    public bool HasOnlineAccess => !string.IsNullOrWhiteSpace(OnlineUrl);

    public static Result<EventVenue> Create(
        string? name = null,
        string? addressLine1 = null,
        string? addressLine2 = null,
        string? landmark = null,
        string? postalCode = null,
        double? latitude = null,
        double? longitude = null,
        string? onlineUrl = null)
    {
        // A half-supplied coordinate is worse than none: it puts a pin on the equator.
        if ((latitude is null) != (longitude is null))
        {
            latitude = null;
            longitude = null;
        }

        if (latitude is { } lat && (lat < -90 || lat > 90))
        {
            latitude = null;
            longitude = null;
        }

        if (longitude is { } lon && (lon < -180 || lon > 180))
        {
            latitude = null;
            longitude = null;
        }

        var url = Clip(onlineUrl, CommunityLifeLimits.MaxUrl);

        if (url is not null && !ContactDetails.IsAbsoluteHttpUrl(url))
        {
            url = null;
        }

        return Result.Success(new EventVenue(
            Clip(name, CommunityLifeLimits.MaxDisplayName),
            Clip(addressLine1, CommunityLifeLimits.MaxDisplayName),
            Clip(addressLine2, CommunityLifeLimits.MaxDisplayName),
            Clip(landmark, CommunityLifeLimits.MaxDisplayName),
            Clip(postalCode, 16),
            latitude,
            longitude,
            url));
    }

    private static string? Clip(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length > max ? trimmed[..max] : trimmed;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Name;
        yield return AddressLine1;
        yield return AddressLine2;
        yield return Landmark;
        yield return PostalCode;
        yield return Latitude;
        yield return Longitude;
        yield return OnlineUrl;
    }
}
