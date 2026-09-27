using System.ComponentModel.DataAnnotations;

namespace Diaspora.Maps.Google;

/// <summary>
/// <c>Maps:Google</c>. One server key for the Routes and Places web services.
/// <para>
/// <b>The allowances are the budget, and their ceilings are Google's free caps.</b> The user's
/// decision (27 Sep 2026) is free tier only. Each allowance's <see cref="RangeAttribute"/> is the
/// SKU's free monthly cap as published on 27 Sep 2026, so a deployment cannot configure its way
/// into billing. The defaults leave a tenth of each cap as margin for calls counted by Google but
/// not by us: a request that timed out here but reached Google, for example.
/// </para>
/// <para>
/// <b>The SKUs this adapter can reach, and only these.</b> Compute Routes Essentials (never a
/// traffic-aware preference, never more than ten waypoints, never two-wheeler, any of which moves a
/// request to Pro or Enterprise); Autocomplete Requests; Place Details Essentials (the field mask
/// never includes <c>displayName</c>, which is Pro).
/// </para>
/// </summary>
public sealed class GoogleMapsOptions
{
    public const string SectionName = "Maps:Google";

    /// <summary>
    /// A server key restricted to the Routes API and Places API (New), and by IP where the host
    /// allows it. Never the Android map key: that one ships in the APK and is extractable.
    /// </summary>
    public string? ApiKey { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

    public bool EnableRouting { get; set; } = true;

    /// <summary>
    /// Public transport itineraries. On by default, because transit is an Essentials request with no
    /// waypoints, but kept separable: coverage outside Europe and North America is thin.
    /// </summary>
    public bool EnableTransit { get; set; } = true;

    public bool EnablePlaces { get; set; } = true;

    public string RoutesBaseUrl { get; set; } = "https://routes.googleapis.com/";

    public string PlacesBaseUrl { get; set; } = "https://places.googleapis.com/";

    [Range(1, 30)]
    public int TimeoutSeconds { get; set; } = 8;

    /// <summary>Google Maps Platform bills by calendar month in Pacific time.</summary>
    [Required]
    public string BillingTimeZone { get; set; } = "America/Los_Angeles";

    /// <summary>Compute Routes Essentials: 10,000 free a month.</summary>
    [Range(0, 10_000)]
    public int RoutesMonthlyAllowance { get; set; } = 9_000;

    /// <summary>
    /// Autocomplete Requests: 10,000 free a month.
    /// <para>
    /// Every request is counted, although Google bills only a session's first twelve when it ends in
    /// a pick. The meter cannot know at request time whether a member will pick or give up, and an
    /// abandoned session bills every request, so counting all of them is the only safe answer.
    /// </para>
    /// </summary>
    [Range(0, 10_000)]
    public int AutocompleteMonthlyAllowance { get; set; } = 9_000;

    /// <summary>Place Details Essentials: 10,000 free a month.</summary>
    [Range(0, 10_000)]
    public int PlaceDetailsMonthlyAllowance { get; set; } = 9_000;
}
