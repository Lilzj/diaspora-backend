using Diaspora.SharedKernel.Primitives;

namespace Diaspora.UnitTests.Persistence;

/// <summary>
/// An owned value object sharing its owner's table, with not-null columns.
/// <para>
/// This is the shape that made D-1 fail loudly: a cascade-deleted owned reference on a shared
/// table is expressed as <c>SET city = NULL, postcode = NULL</c>, and the constraint refuses.
/// </para>
/// </summary>
public sealed class SoftDeleteTestAddress : ValueObject
{
    private SoftDeleteTestAddress() { }

    private SoftDeleteTestAddress(string city, string postcode)
    {
        City = city;
        Postcode = postcode;
    }

    public string City { get; private set; } = default!;

    public string Postcode { get; private set; } = default!;

    public static SoftDeleteTestAddress Create(string city, string postcode) =>
        new(city, postcode);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return City;
        yield return Postcode;
    }
}
