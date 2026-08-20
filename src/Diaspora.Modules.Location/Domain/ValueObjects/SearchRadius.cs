using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Domain.ValueObjects;

/// <summary>
/// How wide a discovery query reaches: either a real distance, or the whole containing city
/// or country.
/// <para>
/// The frontend encodes the two non-metric options as <c>-1</c> and <c>-2</c> on the same
/// numeric field as the metric ones. That works in a client that only ever compares the
/// value to itself, and fails the moment it reaches a database: a negative radius matches
/// nothing and reports no error. <see cref="FromClientValue"/> accepts the sentinels and
/// translates them here, at the one boundary that understands them, so the client keeps
/// working unchanged and nothing downstream ever sees a negative distance.
/// </para>
/// </summary>
public sealed class SearchRadius : ValueObject
{
    /// <summary>The frontend's "City-wide" sentinel.</summary>
    public const double CityWideSentinel = -1d;

    /// <summary>The frontend's "Country-wide" sentinel.</summary>
    public const double CountryWideSentinel = -2d;

    /// <summary>The default when a client sends nothing. Matches the store's initial value.</summary>
    public const double DefaultKilometers = 5d;

    /// <summary>
    /// The largest radius accepted. Beyond this the query is a country-level browse in all
    /// but name, and letting it through as a radius means scanning the catalogue while
    /// pretending to be a proximity search.
    /// </summary>
    public const double MaxKilometers = 500d;

    public const double MinKilometers = 0.05d;

    public RadiusScope Scope { get; private set; }

    /// <summary>The distance, meaningful only when <see cref="Scope"/> is
    /// <see cref="RadiusScope.Radius"/>.</summary>
    public Distance Distance { get; private set; } = default!;

    // EF Core constructor. Distance is an owned navigation, so it cannot be bound through the
    // constructor and the setters have to be reachable.
    private SearchRadius() { }

    private SearchRadius(RadiusScope scope, Distance distance)
    {
        Scope = scope;
        Distance = distance;
    }

    public static SearchRadius CityWide => new(RadiusScope.City, Distance.Zero);

    public static SearchRadius CountryWide => new(RadiusScope.Country, Distance.Zero);

    public static SearchRadius Default =>
        new(RadiusScope.Radius, Distance.FromKilometers(DefaultKilometers));

    public static Result<SearchRadius> FromKilometers(double kilometers)
    {
        if (double.IsNaN(kilometers) || double.IsInfinity(kilometers))
        {
            return Result.Failure<SearchRadius>(Error.Validation(
                LocationErrorCodes.RadiusInvalid, "A radius must be a finite number."));
        }

        if (kilometers is < MinKilometers or > MaxKilometers)
        {
            return Result.Failure<SearchRadius>(Error.Validation(
                LocationErrorCodes.RadiusInvalid,
                $"A radius must be between {MinKilometers} and {MaxKilometers} kilometres."));
        }

        return Result.Success(new SearchRadius(RadiusScope.Radius, Distance.FromKilometers(kilometers)));
    }

    /// <summary>
    /// Interprets the value the frontend's <c>RadiusSlider</c> produces, sentinels included.
    /// <para>
    /// A null value means the client did not express a preference, which is not the same as
    /// asking for zero — it gets the default the store would have used.
    /// </para>
    /// </summary>
    public static Result<SearchRadius> FromClientValue(double? value)
    {
        if (value is null)
        {
            return Result.Success(Default);
        }

        // Compared with a tolerance rather than by equality: the value arrives as a JSON
        // number and round-trips through double, and an exact match on -1 is the kind of
        // assumption that holds until it does not.
        if (Math.Abs(value.Value - CityWideSentinel) < 0.001d)
        {
            return Result.Success(CityWide);
        }

        return Math.Abs(value.Value - CountryWideSentinel) < 0.001d
            ? Result.Success(CountryWide)
            : FromKilometers(value.Value);
    }

    /// <summary>The value the frontend's slider would show for this radius.</summary>
    public double ToClientValue() => Scope switch
    {
        RadiusScope.City => CityWideSentinel,
        RadiusScope.Country => CountryWideSentinel,
        _ => Distance.Kilometers
    };

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Scope;
        yield return Distance;
    }

    public override string ToString() => Scope switch
    {
        RadiusScope.City => "city-wide",
        RadiusScope.Country => "country-wide",
        _ => Distance.ToString()
    };
}
