using Diaspora.SharedKernel.Primitives;

namespace Diaspora.SharedKernel.ValueObjects;

public sealed class Coordinates : ValueObject
{
    public double Latitude { get; }
    public double Longitude { get; }

    private Coordinates(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    public static Coordinates Create(double latitude, double longitude)
    {
        Guard.Against.OutOfRange(latitude, nameof(latitude), -90, 90);
        Guard.Against.OutOfRange(longitude, nameof(longitude), -180, 180);
        return new Coordinates(latitude, longitude);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Math.Round(Latitude, 8);
        yield return Math.Round(Longitude, 8);
    }

    public override string ToString() => $"({Latitude}, {Longitude})";
}
