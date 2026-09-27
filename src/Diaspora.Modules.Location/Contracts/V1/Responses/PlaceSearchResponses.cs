namespace Diaspora.Modules.Location.Contracts.V1.Responses;

/// <summary>
/// Street-address suggestions, or the deployment's reason for having none.
/// <para>
/// Always HTTP 200 with a <see cref="Status"/>, the shape <c>POST /geo/routes</c> uses: a vendor
/// that is down, unconfigured or out of allowance is an ordinary answer for a search box, and a
/// status lets the client keep the catalogue results it already has on screen.
/// </para>
/// </summary>
public sealed record PlaceAutocompleteResponse
{
    /// <summary><c>Success</c>, <c>NotConfigured</c>, <c>Unavailable</c>, <c>RateLimited</c>, <c>AllowanceExhausted</c>, …</summary>
    public required string Status { get; init; }

    public required string Provider { get; init; }

    public required IReadOnlyList<PlaceSuggestionResponse> Suggestions { get; init; }

    /// <summary>Credit the provider requires beside its results. Null when there are none.</summary>
    public string? Attribution { get; init; }

    public string? Message { get; init; }
}

public sealed record PlaceSuggestionResponse
{
    public required string PlaceId { get; init; }

    public required string Text { get; init; }

    public required string MainText { get; init; }

    public string? SecondaryText { get; init; }

    public required IReadOnlyList<string> Types { get; init; }
}

public sealed record PlaceDetailsResponse
{
    public required string Status { get; init; }

    public required string Provider { get; init; }

    /// <summary>Null unless <see cref="Status"/> is <c>Success</c>.</summary>
    public PlaceDetailResponse? Place { get; init; }

    public string? Attribution { get; init; }

    public string? Message { get; init; }
}

public sealed record PlaceDetailResponse
{
    public required string PlaceId { get; init; }

    public required double Latitude { get; init; }

    public required double Longitude { get; init; }

    public string? Name { get; init; }

    public string? FormattedAddress { get; init; }

    public string? CountryCode { get; init; }

    public string? Locality { get; init; }

    public string? PostalCode { get; init; }
}
