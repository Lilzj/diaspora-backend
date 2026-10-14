using Diaspora.Modules.Location.Domain.ValueObjects;

namespace Diaspora.Modules.Location.Domain.Places;

/// <summary>
/// Street addresses and points of interest, from a vendor.
/// <para>
/// <b>Why this is separate from <see cref="Spatial.IGeocodingProvider"/>.</b> That seam answers
/// "which catalogue area is this", and filing, search and the neighbourhood hub are built on its
/// answer carrying an area id. A vendor cannot answer that question and should not be asked to.
/// This seam answers the one the catalogue cannot: "which street address or building does the
/// member mean". Replacing the catalogue geocoder with a vendor would have lost the area id
/// everything downstream relies on.
/// </para>
/// <para>
/// <b>Provider-neutral, like the routing seam.</b> No vendor name, URL or credential appears here.
/// An adapter outside the module implements it and registers after the module does.
/// </para>
/// <para>
/// <b>Nothing a provider returns is stored.</b> Vendor terms restrict caching of place content,
/// and the platform has no reason to keep it: a member picking an address copies the coordinate
/// into whatever they are creating, which is their own data from then on.
/// </para>
/// </summary>
public interface IPlaceSearchProvider
{
    /// <summary>A stable, public-safe key such as <c>none</c> or <c>google</c>.</summary>
    string Key { get; }

    /// <summary>Whether this deployment has a provider at all. The capability report reads it.</summary>
    bool IsConfigured { get; }

    /// <summary>The credit the provider's licence requires beside its results, or null.</summary>
    string? Attribution => null;

    /// <summary>
    /// Suggestions for what a member has typed so far. Never throws for a provider condition.
    /// </summary>
    Task<PlaceSearchResult<IReadOnlyList<PlaceSuggestion>>> AutocompleteAsync(
        PlaceAutocompleteRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// The coordinate and address of a suggestion the member picked, which closes the session.
    /// </summary>
    Task<PlaceSearchResult<PlaceDetail>> GetDetailsAsync(
        PlaceDetailsRequest request, CancellationToken cancellationToken = default);
}

/// <summary>What a member has typed, and the context that narrows it.</summary>
public sealed record PlaceAutocompleteRequest
{
    public required string Input { get; init; }

    /// <summary>
    /// One member's typing, from the first keystroke to the pick. Vendors bill a session rather
    /// than each keystroke when it is supplied, so it is required rather than optional.
    /// </summary>
    public required string SessionToken { get; init; }

    /// <summary>Where results should lean towards. A bias, never a filter.</summary>
    public GeoCoordinate? Near { get; init; }

    public int? NearRadiusMetres { get; init; }

    /// <summary>ISO 3166-1 alpha-2, upper case. Empty means anywhere.</summary>
    public IReadOnlyList<string> CountryCodes { get; init; } = [];

    /// <summary>BCP-47. Passed through, never interpreted.</summary>
    public string? Language { get; init; }

    /// <summary>
    /// Towns and cities only. Added 14 Oct 2026 for the one town picker every form uses, so a
    /// member in any country finds their town, not a street or a shop of the same name.
    /// </summary>
    public bool TownsOnly { get; init; }

    /// <summary>
    /// Countries, regions, towns and districts, not streets or businesses: "where am I looking",
    /// the location sheet's question (14 Oct 2026). Ignored when <see cref="TownsOnly"/> is set.
    /// </summary>
    public bool RegionsOnly { get; init; }
}

public sealed record PlaceDetailsRequest
{
    public required string PlaceId { get; init; }

    /// <summary>The token the suggestions were fetched with. Ends the session.</summary>
    public required string SessionToken { get; init; }

    public string? Language { get; init; }
}

/// <summary>One suggestion, as the vendor worded it.</summary>
public sealed record PlaceSuggestion
{
    /// <summary>The vendor's opaque id. The only thing the client sends back.</summary>
    public required string PlaceId { get; init; }

    public required string Text { get; init; }

    public required string MainText { get; init; }

    public string? SecondaryText { get; init; }

    public IReadOnlyList<string> Types { get; init; } = [];
}

/// <summary>A picked place: where it is, and how the vendor writes its address.</summary>
public sealed record PlaceDetail
{
    public required string PlaceId { get; init; }

    public required GeoCoordinate Coordinate { get; init; }

    public string? Name { get; init; }

    public string? FormattedAddress { get; init; }

    public string? CountryCode { get; init; }

    public string? Locality { get; init; }

    public string? PostalCode { get; init; }
}

public enum PlaceSearchOutcome
{
    Success = 0,
    NotConfigured = 1,
    Unavailable = 2,
    RateLimited = 3,
    InvalidRequest = 4,
    NotFound = 5,

    /// <summary>
    /// This month's free allowance is spent. Separate from <see cref="RateLimited"/> because the
    /// honest advice differs: "try again in a moment" is wrong when the answer is "next month".
    /// </summary>
    AllowanceExhausted = 6
}

/// <summary>
/// What a provider answered. Built only through the factories, so a success always carries a
/// value and a failure never does.
/// </summary>
public sealed record PlaceSearchResult<T>
    where T : class
{
    private PlaceSearchResult(PlaceSearchOutcome outcome, string providerKey, T? value)
    {
        Outcome = outcome;
        ProviderKey = providerKey;
        Value = value;
    }

    public PlaceSearchOutcome Outcome { get; }

    public string ProviderKey { get; }

    public T? Value { get; }

    public bool IsSuccess => Outcome == PlaceSearchOutcome.Success;

    public static PlaceSearchResult<T> Success(string providerKey, T value) =>
        new(PlaceSearchOutcome.Success, providerKey, value ?? throw new ArgumentNullException(nameof(value)));

    public static PlaceSearchResult<T> Failed(PlaceSearchOutcome outcome, string providerKey) =>
        outcome == PlaceSearchOutcome.Success
            ? throw new ArgumentException("A failure cannot be a success.", nameof(outcome))
            : new(outcome, providerKey, null);
}
