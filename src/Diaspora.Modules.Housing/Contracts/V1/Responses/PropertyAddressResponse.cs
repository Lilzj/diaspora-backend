namespace Diaspora.Modules.Housing.Contracts.V1.Responses;

/// <summary>
/// As much of the address as this viewer is entitled to.
/// <para>
/// <b>Always the redacted copy.</b> Every mapper builds this from
/// <c>PropertyAddress.Disclose</c>, which takes who is asking and returns filtered data rather than
/// a permission flag — so there is no path by which a street line reaches this record without the
/// rule having been applied. An architecture test asserts the raw value object is never mapped
/// directly.
/// </para>
/// <para>
/// Below full disclosure the coordinate is rounded to roughly a kilometre: a map with no pin is
/// useless for judging a commute, and an exact pin on a public page is a door number in another
/// form.
/// </para>
/// </summary>
public sealed record PropertyAddressResponse
{
    /// <summary>The street, where the viewer may see it. Null otherwise.</summary>
    public string? Line1 { get; init; }

    public string? Line2 { get; init; }

    public string? PostalCode { get; init; }

    /// <summary>Exact only at full disclosure. Fuzzed below it.</summary>
    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    /// <summary>What the landlord chose to publish.</summary>
    public required string Visibility { get; init; }

    /// <summary>Whether this viewer is seeing the full address.</summary>
    public required bool IsFullyDisclosed { get; init; }

    /// <summary>
    /// How to get the rest, where there is a way. Rendered as a prompt rather than an error,
    /// because booking a viewing is the intended path to an address.
    /// </summary>
    public string? DisclosureHint { get; init; }
}
