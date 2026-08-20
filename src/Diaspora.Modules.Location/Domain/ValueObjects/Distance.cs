using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Location.Domain.ValueObjects;

/// <summary>
/// A non-negative distance, stored in kilometres and rendered in whatever unit the member reads.
/// <para>
/// One canonical unit is the whole point. The frontend's <c>distanceKm</c> field name is on
/// every listing DTO in the platform, and a module that started returning miles under the
/// same name would break every comparison silently rather than loudly. Kilometres in,
/// kilometres stored, kilometres on the wire; <see cref="In"/> exists for display and for
/// nothing else.
/// </para>
/// </summary>
public sealed class Distance : ValueObject, IComparable<Distance>
{
    private const double KilometersPerMile = 1.609344d;

    public double Kilometers { get; }

    private Distance(double kilometers)
    {
        Kilometers = kilometers;
    }

    public static Distance Zero => new(0d);

    public static Distance FromKilometers(double kilometers)
    {
        if (double.IsNaN(kilometers) || double.IsInfinity(kilometers))
        {
            throw new ArgumentOutOfRangeException(
                nameof(kilometers), "A distance must be a finite number.");
        }

        if (kilometers < 0d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(kilometers), "A distance cannot be negative.");
        }

        return new Distance(kilometers);
    }

    public static Distance FromMeters(double meters) => FromKilometers(meters / 1000d);

    public static Distance FromMiles(double miles) => FromKilometers(miles * KilometersPerMile);

    public double Meters => Kilometers * 1000d;

    public double Miles => Kilometers / KilometersPerMile;

    /// <summary>This distance expressed in the requested unit. Display only.</summary>
    public double In(DistanceUnit unit) => unit switch
    {
        DistanceUnit.Miles => Miles,
        _ => Kilometers
    };

    public int CompareTo(Distance? other) =>
        other is null ? 1 : Kilometers.CompareTo(other.Kilometers);

    public static bool operator <(Distance? left, Distance? right) =>
        left is null ? right is not null : left.CompareTo(right) < 0;

    public static bool operator >(Distance? left, Distance? right) =>
        left is not null && left.CompareTo(right) > 0;

    public static bool operator <=(Distance? left, Distance? right) =>
        left is null || left.CompareTo(right) <= 0;

    public static bool operator >=(Distance? left, Distance? right) =>
        left is null ? right is null : left.CompareTo(right) >= 0;

    // Declared on the concrete type rather than inherited from ValueObject so that equality
    // and the ordering operators above resolve through the same overload set. Without these,
    // `a == b` binds to the base's ValueObject overload while `a < b` binds here, which is
    // exactly the asymmetry CA1036 exists to catch.
    public static bool operator ==(Distance? left, Distance? right) => Equals(left, right);

    public static bool operator !=(Distance? left, Distance? right) => !Equals(left, right);

    public override bool Equals(object? obj) => base.Equals(obj);

    public override int GetHashCode() => base.GetHashCode();

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        // Rounded to the metre: two distances that differ by less than that differ only in
        // floating-point noise, and treating them as unequal would make every distance
        // comparison in the module unstable.
        yield return Math.Round(Kilometers, 3);
    }

    public override string ToString() =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{Kilometers:F3} km");
}
