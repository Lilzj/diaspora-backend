namespace Diaspora.Modules.Location.Contracts.V1.Responses;

/// <summary>
/// A country as it appears in a list.
/// <para>
/// A superset of both frontend country shapes: <c>CountryItem</c> from the bundled list
/// (code, name, flag, callingCode) and <c>CountryRef</c> from the guide types (code, name,
/// flag). One response binds to both, which is why neither name survives here.
/// </para>
/// </summary>
public sealed record CountrySummaryResponse
{
    public required Guid Id { get; init; }

    /// <summary>ISO 3166-1 alpha-2. The frontend's <c>code</c> and <c>countryCode</c>.</summary>
    public required string Code { get; init; }

    public required string Name { get; init; }

    public required string Flag { get; init; }

    /// <summary>E.164, with the leading plus. Read by the phone input.</summary>
    public string? CallingCode { get; init; }

    /// <summary>ISO 4217.</summary>
    public string? Currency { get; init; }

    /// <summary>Whether Diaspora operates here. Listing is not operating.</summary>
    public required bool IsSupported { get; init; }

    /// <summary>Surfaced above the alphabetical list on selection screens.</summary>
    public required bool IsFeatured { get; init; }
}
