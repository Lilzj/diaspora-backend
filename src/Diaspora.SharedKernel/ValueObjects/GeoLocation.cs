using Diaspora.SharedKernel.Primitives;

namespace Diaspora.SharedKernel.ValueObjects;

public sealed class GeoLocation : ValueObject
{
    public Coordinates Coordinates { get; }
    public string? Address { get; }
    public string? City { get; }
    public string? Country { get; }

    private GeoLocation(Coordinates coordinates, string? address, string? city, string? country)
    {
        Coordinates = coordinates;
        Address = address;
        City = city;
        Country = country;
    }

    public static GeoLocation Create(Coordinates coordinates, string? address = null, string? city = null, string? country = null) =>
        new(coordinates, address, city, country);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Coordinates;
        yield return Address;
        yield return City;
        yield return Country;
    }
}
