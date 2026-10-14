using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Diaspora.Modules.Location.Domain.Places;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Platform.Usage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Maps.Google;

/// <summary>
/// Places API (New) autocomplete and details, behind the Location module's place search seam.
/// <para>
/// <b>Why details never asks for a name.</b> <c>displayName</c> moves Place Details from the
/// Essentials SKU to Pro. The member already saw the suggestion's main text, and the client carries
/// it across to the pick, so the name is not worth a more expensive SKU.
/// </para>
/// <para>
/// <b>Region codes are sent in lower case.</b> Google describes <c>includedRegionCodes</c> as
/// two-character region values. The one where ISO and ccTLD disagree, GB/uk, is exercised live
/// before this is trusted. See <see cref="RegionCode"/>.
/// </para>
/// </summary>
internal sealed class GooglePlaceSearchProvider : IPlaceSearchProvider
{
    public const string HttpClientName = "maps.google.places";
    public const string ProviderKey = "google";
    public const string AutocompleteSku = "places-autocomplete-requests";
    public const string DetailsSku = "places-details-essentials";

    internal const string AutocompleteFieldMask =
        "suggestions.placePrediction.placeId,suggestions.placePrediction.text.text,"
        + "suggestions.placePrediction.structuredFormat,suggestions.placePrediction.types";

    /// <summary>Essentials only. Adding <c>displayName</c> here would bill every pick as Pro.</summary>
    internal const string DetailsFieldMask = "id,location,formattedAddress,addressComponents";

    /// <summary>Bias radius when a member gives a point and no radius.</summary>
    private const int DefaultBiasMetres = 20_000;

    private readonly HttpClient _http;
    private readonly IProviderUsageMeter _meter;
    private readonly IOptionsMonitor<GoogleMapsOptions> _options;
    private readonly ILogger<GooglePlaceSearchProvider> _logger;

    public GooglePlaceSearchProvider(
        HttpClient http,
        IProviderUsageMeter meter,
        IOptionsMonitor<GoogleMapsOptions> options,
        ILogger<GooglePlaceSearchProvider> logger)
    {
        _http = http;
        _meter = meter;
        _options = options;
        _logger = logger;
    }

    public string Key => ProviderKey;

    /// <summary>Suggestions are shown away from any map, where Google requires its credit.</summary>
    public string? Attribution => "Google Maps";

    public bool IsConfigured => _options.CurrentValue is { IsConfigured: true, EnablePlaces: true };

    public async Task<PlaceSearchResult<IReadOnlyList<PlaceSuggestion>>> AutocompleteAsync(
        PlaceAutocompleteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var settings = _options.CurrentValue;

        if (!IsConfigured) return Fail<IReadOnlyList<PlaceSuggestion>>(PlaceSearchOutcome.NotConfigured);

        if (!await _meter.TryConsumeAsync(
                Key, AutocompleteSku, settings.AutocompleteMonthlyAllowance, settings.BillingTimeZone,
                cancellationToken))
        {
            _logger.LogWarning("Google autocomplete allowance for this month is spent; refusing without calling.");
            return Fail<IReadOnlyList<PlaceSuggestion>>(PlaceSearchOutcome.AllowanceExhausted);
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, "v1/places:autocomplete")
        {
            Content = JsonContent.Create(BuildAutocompleteBody(request))
        };

        message.Headers.Add("X-Goog-Api-Key", settings.ApiKey);
        message.Headers.Add("X-Goog-FieldMask", AutocompleteFieldMask);

        var (status, body) = await SendAsync(message, cancellationToken);

        if (status != PlaceSearchOutcome.Success)
        {
            return Fail<IReadOnlyList<PlaceSuggestion>>(status);
        }

        return PlaceSearchResult<IReadOnlyList<PlaceSuggestion>>.Success(Key, ParseSuggestions(body));
    }

    public async Task<PlaceSearchResult<PlaceDetail>> GetDetailsAsync(
        PlaceDetailsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var settings = _options.CurrentValue;

        if (!IsConfigured) return Fail<PlaceDetail>(PlaceSearchOutcome.NotConfigured);

        if (!await _meter.TryConsumeAsync(
                Key, DetailsSku, settings.PlaceDetailsMonthlyAllowance, settings.BillingTimeZone,
                cancellationToken))
        {
            _logger.LogWarning("Google place details allowance for this month is spent; refusing without calling.");
            return Fail<PlaceDetail>(PlaceSearchOutcome.AllowanceExhausted);
        }

        // Both values were validated against URL-safe patterns before reaching here; escaped anyway.
        var path = $"v1/places/{Uri.EscapeDataString(request.PlaceId)}"
                   + $"?sessionToken={Uri.EscapeDataString(request.SessionToken)}"
                   + (string.IsNullOrWhiteSpace(request.Language)
                       ? string.Empty
                       : $"&languageCode={Uri.EscapeDataString(request.Language)}");

        using var message = new HttpRequestMessage(HttpMethod.Get, path);

        message.Headers.Add("X-Goog-Api-Key", settings.ApiKey);
        message.Headers.Add("X-Goog-FieldMask", DetailsFieldMask);

        var (status, body) = await SendAsync(message, cancellationToken);

        if (status != PlaceSearchOutcome.Success)
        {
            return Fail<PlaceDetail>(status);
        }

        var detail = ParseDetail(body, request.PlaceId);

        return detail is null
            ? Fail<PlaceDetail>(PlaceSearchOutcome.NotFound)
            : PlaceSearchResult<PlaceDetail>.Success(Key, detail);
    }

    private async Task<(PlaceSearchOutcome Status, JsonObject? Body)> SendAsync(
        HttpRequestMessage message, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;

        try
        {
            response = await _http.SendAsync(message, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return (PlaceSearchOutcome.Unavailable, null);
        }

        using (response)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    return (PlaceSearchOutcome.Success,
                        await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken));
                case HttpStatusCode.NotFound:
                    return (PlaceSearchOutcome.NotFound, null);
                case HttpStatusCode.TooManyRequests:
                    return (PlaceSearchOutcome.RateLimited, null);
                case HttpStatusCode.BadRequest:
                    return (PlaceSearchOutcome.InvalidRequest, null);
                case HttpStatusCode.Unauthorized:
                case HttpStatusCode.Forbidden:
                    _logger.LogError(
                        "Google Places refused the server key ({Status}). Check the key's API restrictions.",
                        (int)response.StatusCode);
                    return (PlaceSearchOutcome.Unavailable, null);
                default:
                    return (PlaceSearchOutcome.Unavailable, null);
            }
        }
    }

    internal static JsonObject BuildAutocompleteBody(PlaceAutocompleteRequest request)
    {
        var body = new JsonObject
        {
            ["input"] = request.Input,
            ["sessionToken"] = request.SessionToken
        };

        if (request.Near is { } near)
        {
            body["locationBias"] = new JsonObject
            {
                ["circle"] = new JsonObject
                {
                    ["center"] = new JsonObject
                    {
                        ["latitude"] = near.Latitude,
                        ["longitude"] = near.Longitude
                    },
                    ["radius"] = (double)(request.NearRadiusMetres ?? DefaultBiasMetres)
                }
            };
        }

        if (request.CountryCodes.Count > 0)
        {
            body["includedRegionCodes"] = new JsonArray(
                [.. request.CountryCodes.Select(code => (JsonNode)JsonValue.Create(RegionCode(code))!)]);
        }

        // Google's own collection of town-like types (localities and their equivalents). The
        // Places API takes "(cities)" here instead of listing them.
        if (request.TownsOnly)
        {
            body["includedPrimaryTypes"] = new JsonArray(JsonValue.Create("(cities)"));
        }

        if (!string.IsNullOrWhiteSpace(request.Language))
        {
            body["languageCode"] = request.Language;
        }

        return body;
    }

    /// <summary>The platform's ISO 3166 code as Google's region value.</summary>
    internal static string RegionCode(string isoCode) => isoCode.ToLowerInvariant();

    internal static IReadOnlyList<PlaceSuggestion> ParseSuggestions(JsonObject? body)
    {
        if (body?["suggestions"] is not JsonArray suggestions) return [];

        var parsed = new List<PlaceSuggestion>();

        // Query predictions ("pizza near me") are skipped: there is nothing to pick.
        foreach (var prediction in suggestions.Select(entry => entry?["placePrediction"]).OfType<JsonObject>())
        {
            var placeId = Text(prediction["placeId"]);
            var text = Text(prediction["text"]?["text"]);

            if (placeId is null || text is null) continue;

            var structured = prediction["structuredFormat"];

            parsed.Add(new PlaceSuggestion
            {
                PlaceId = placeId,
                Text = text,
                MainText = Text(structured?["mainText"]?["text"]) ?? text,
                SecondaryText = Text(structured?["secondaryText"]?["text"]),
                Types = [.. (prediction["types"] as JsonArray ?? []).Select(Text).OfType<string>()]
            });
        }

        return parsed;
    }

    internal static PlaceDetail? ParseDetail(JsonObject? body, string requestedId)
    {
        var location = body?["location"];

        if (location is null
            || GeoCoordinate.TryCreate(Number(location["latitude"]), Number(location["longitude"])) is not
                { IsSuccess: true } coordinate)
        {
            return null;
        }

        var components = (body!["addressComponents"] as JsonArray ?? []).OfType<JsonObject>().ToList();

        string? Component(string type, string field) => components
            .Where(component => (component["types"] as JsonArray ?? [])
                .Any(entry => Text(entry) == type))
            .Select(component => Text(component[field]))
            .FirstOrDefault(value => value is not null);

        return new PlaceDetail
        {
            PlaceId = Text(body["id"]) ?? requestedId,
            Coordinate = coordinate.Value,
            FormattedAddress = Text(body["formattedAddress"]),
            CountryCode = Component("country", "shortText")?.ToUpperInvariant(),
            // UK addresses name the town as the postal town; elsewhere it is the locality.
            Locality = Component("locality", "longText") ?? Component("postal_town", "longText"),
            PostalCode = Component("postal_code", "longText")
        };
    }

    private PlaceSearchResult<T> Fail<T>(PlaceSearchOutcome outcome)
        where T : class =>
        PlaceSearchResult<T>.Failed(outcome, Key);

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text)
            ? text
            : null;

    private static double Number(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<double>(out var number) ? number : double.NaN;
}
