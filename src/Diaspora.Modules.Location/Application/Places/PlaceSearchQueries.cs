using System.Text.RegularExpressions;
using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Contracts.V1.Responses;
using Diaspora.Modules.Location.Domain.Places;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Location.Application.Places;

/// <summary>What a member has typed into an address field.</summary>
[Authorize(Permission = LocationPermissions.Search)]
public sealed record AutocompletePlacesQuery : IQuery<PlaceAutocompleteResponse>
{
    public required string Input { get; init; }

    public required string SessionToken { get; init; }

    public double? NearLatitude { get; init; }

    public double? NearLongitude { get; init; }

    public int? NearRadiusMetres { get; init; }

    public IReadOnlyList<string> CountryCodes { get; init; } = [];

    public string? Language { get; init; }

    /// <summary>Towns and cities only, not streets or businesses (14 Oct 2026, the town picker).</summary>
    public bool TownsOnly { get; init; }
}

/// <summary>The suggestion a member picked.</summary>
[Authorize(Permission = LocationPermissions.Search)]
public sealed record GetPlaceDetailsQuery : IQuery<PlaceDetailsResponse>
{
    public required string PlaceId { get; init; }

    public required string SessionToken { get; init; }

    public string? Language { get; init; }
}

/// <summary>
/// Validation, the per-member fence and the timeout, shared by both place queries.
/// <para>
/// Everything cheap happens before the vendor is asked, the order <c>PlanRouteQueryHandler</c>
/// keeps: a malformed request costs a regex, not a unit of the month's allowance.
/// </para>
/// </summary>
internal static partial class PlaceSearchRules
{
    public const int MinimumInput = 2;
    public const int MaximumInput = 120;
    public const int MaximumCountries = 5;
    public const int MaximumBiasRadiusMetres = 50_000;

    /// <summary>
    /// What a session token may look like. A UUID fits, and so does anything URL-safe of a sane
    /// length. The token is forwarded to the vendor, so it is never free text.
    /// </summary>
    [GeneratedRegex("^[A-Za-z0-9_-]{16,64}$")]
    public static partial Regex SessionToken();

    /// <summary>Vendor place ids are URL-safe and bounded. Anything else never leaves the platform.</summary>
    [GeneratedRegex("^[A-Za-z0-9_-]{8,512}$")]
    public static partial Regex PlaceId();

    [GeneratedRegex("^[A-Za-z]{2}$")]
    public static partial Regex CountryCode();

    [GeneratedRegex("^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8})*$")]
    public static partial Regex Language();

    public static Error Invalid(string field, string message) =>
        Error.Validation($"Location.Places.{field}", message);

    public static string? Explain(PlaceSearchOutcome outcome) => outcome switch
    {
        PlaceSearchOutcome.Success => null,
        PlaceSearchOutcome.NotConfigured =>
            "Street address search is not available on Diaspora yet.",
        PlaceSearchOutcome.Unavailable =>
            "Address search is temporarily unavailable. Please try again shortly.",
        PlaceSearchOutcome.RateLimited =>
            "Too many address searches just now. Please try again in a few minutes.",
        PlaceSearchOutcome.AllowanceExhausted =>
            "Address search has reached this month's limit. You can still search towns and neighbourhoods.",
        PlaceSearchOutcome.NotFound => "That place could not be found.",
        _ => "That address search could not be understood."
    };

    /// <summary>
    /// Asks the provider under a timeout, turning anything it throws into a status.
    /// </summary>
    public static async Task<PlaceSearchResult<T>> AskAsync<T>(
        IPlaceSearchProvider provider,
        Func<CancellationToken, Task<PlaceSearchResult<T>>> call,
        int timeoutMilliseconds,
        CancellationToken cancellationToken)
        where T : class
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(timeoutMilliseconds);

        try
        {
            return await call(budget.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return PlaceSearchResult<T>.Failed(PlaceSearchOutcome.Unavailable, provider.Key);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Never the vendor's message: an error body can echo what the member typed.
            return PlaceSearchResult<T>.Failed(PlaceSearchOutcome.Unavailable, provider.Key);
        }
    }

    /// <summary>Whether this member is still inside their own fence.</summary>
    public static async Task<bool> WithinMemberFenceAsync(
        ISubjectRateLimiter limiter,
        ICurrentUser user,
        string purpose,
        int permits,
        int windowMinutes,
        CancellationToken cancellationToken)
    {
        // The policy already requires a signed-in member; this is belt and braces for the key.
        var subject = user.UserId?.ToString() ?? "anonymous";
        var lease = await limiter.AcquireAsync(
            $"places:{purpose}:{subject}", permits, TimeSpan.FromMinutes(windowMinutes), cancellationToken);
        return lease.IsAllowed;
    }
}

public sealed class AutocompletePlacesQueryHandler
    : IQueryHandler<AutocompletePlacesQuery, PlaceAutocompleteResponse>
{
    private readonly IPlaceSearchProvider _provider;
    private readonly ISubjectRateLimiter _limiter;
    private readonly ICurrentUser _user;
    private readonly IOptions<LocationPlacesOptions> _options;

    public AutocompletePlacesQueryHandler(
        IPlaceSearchProvider provider,
        ISubjectRateLimiter limiter,
        ICurrentUser user,
        IOptions<LocationPlacesOptions> options)
    {
        _provider = provider;
        _limiter = limiter;
        _user = user;
        _options = options;
    }

    public async Task<Result<PlaceAutocompleteResponse>> Handle(
        AutocompletePlacesQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var input = (request.Input ?? string.Empty).Trim();

        if (input.Length < PlaceSearchRules.MinimumInput || input.Length > PlaceSearchRules.MaximumInput)
        {
            return Result.Failure<PlaceAutocompleteResponse>(PlaceSearchRules.Invalid(
                "Input",
                $"Type between {PlaceSearchRules.MinimumInput} and {PlaceSearchRules.MaximumInput} characters."));
        }

        if (!PlaceSearchRules.SessionToken().IsMatch(request.SessionToken ?? string.Empty))
        {
            return Result.Failure<PlaceAutocompleteResponse>(PlaceSearchRules.Invalid(
                "SessionToken", "A session token of 16 to 64 URL-safe characters is required."));
        }

        if (request.CountryCodes.Count > PlaceSearchRules.MaximumCountries
            || request.CountryCodes.Any(code => !PlaceSearchRules.CountryCode().IsMatch(code ?? string.Empty)))
        {
            return Result.Failure<PlaceAutocompleteResponse>(PlaceSearchRules.Invalid(
                "CountryCodes", $"Up to {PlaceSearchRules.MaximumCountries} two-letter country codes."));
        }

        if (request.Language is { } language && !PlaceSearchRules.Language().IsMatch(language))
        {
            return Result.Failure<PlaceAutocompleteResponse>(PlaceSearchRules.Invalid(
                "Language", "The language must be a BCP-47 tag such as en or en-GB."));
        }

        GeoCoordinate? near = null;

        if (request.NearLatitude is not null || request.NearLongitude is not null)
        {
            var point = GeoCoordinate.TryCreate(
                request.NearLatitude ?? double.NaN, request.NearLongitude ?? double.NaN);

            if (point.IsFailure)
            {
                return Result.Failure<PlaceAutocompleteResponse>(point.Error);
            }

            near = point.Value;
        }

        var settings = _options.Value;

        var answer = await PlaceSearchRules.WithinMemberFenceAsync(
                _limiter, _user, "autocomplete", settings.AutocompletePerMember,
                settings.MemberWindowMinutes, cancellationToken)
            ? await PlaceSearchRules.AskAsync(
                _provider,
                token => _provider.AutocompleteAsync(
                    new PlaceAutocompleteRequest
                    {
                        Input = input,
                        SessionToken = request.SessionToken!,
                        Near = near,
                        NearRadiusMetres = request.NearRadiusMetres is { } radius
                            ? Math.Clamp(radius, 100, PlaceSearchRules.MaximumBiasRadiusMetres)
                            : null,
                        CountryCodes = [.. request.CountryCodes.Select(code => code.ToUpperInvariant()).Distinct()],
                        Language = request.Language,
                        TownsOnly = request.TownsOnly
                    },
                    token),
                settings.TimeoutMilliseconds,
                cancellationToken)
            : PlaceSearchResult<IReadOnlyList<PlaceSuggestion>>.Failed(
                PlaceSearchOutcome.RateLimited, _provider.Key);

        return Result.Success(new PlaceAutocompleteResponse
        {
            Status = answer.Outcome.ToString(),
            Provider = answer.ProviderKey,
            Suggestions =
            [
                .. (answer.Value ?? []).Select(suggestion => new PlaceSuggestionResponse
                {
                    PlaceId = suggestion.PlaceId,
                    Text = suggestion.Text,
                    MainText = suggestion.MainText,
                    SecondaryText = suggestion.SecondaryText,
                    Types = suggestion.Types
                })
            ],
            Attribution = answer.IsSuccess ? _provider.Attribution : null,
            Message = PlaceSearchRules.Explain(answer.Outcome)
        });
    }
}

public sealed class GetPlaceDetailsQueryHandler
    : IQueryHandler<GetPlaceDetailsQuery, PlaceDetailsResponse>
{
    private readonly IPlaceSearchProvider _provider;
    private readonly ISubjectRateLimiter _limiter;
    private readonly ICurrentUser _user;
    private readonly IOptions<LocationPlacesOptions> _options;

    public GetPlaceDetailsQueryHandler(
        IPlaceSearchProvider provider,
        ISubjectRateLimiter limiter,
        ICurrentUser user,
        IOptions<LocationPlacesOptions> options)
    {
        _provider = provider;
        _limiter = limiter;
        _user = user;
        _options = options;
    }

    public async Task<Result<PlaceDetailsResponse>> Handle(
        GetPlaceDetailsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!PlaceSearchRules.PlaceId().IsMatch(request.PlaceId ?? string.Empty))
        {
            return Result.Failure<PlaceDetailsResponse>(PlaceSearchRules.Invalid(
                "PlaceId", "That place id is not one this platform issued."));
        }

        if (!PlaceSearchRules.SessionToken().IsMatch(request.SessionToken ?? string.Empty))
        {
            return Result.Failure<PlaceDetailsResponse>(PlaceSearchRules.Invalid(
                "SessionToken", "A session token of 16 to 64 URL-safe characters is required."));
        }

        if (request.Language is { } language && !PlaceSearchRules.Language().IsMatch(language))
        {
            return Result.Failure<PlaceDetailsResponse>(PlaceSearchRules.Invalid(
                "Language", "The language must be a BCP-47 tag such as en or en-GB."));
        }

        var settings = _options.Value;

        var answer = await PlaceSearchRules.WithinMemberFenceAsync(
                _limiter, _user, "details", settings.DetailsPerMember,
                settings.MemberWindowMinutes, cancellationToken)
            ? await PlaceSearchRules.AskAsync(
                _provider,
                token => _provider.GetDetailsAsync(
                    new PlaceDetailsRequest
                    {
                        PlaceId = request.PlaceId!,
                        SessionToken = request.SessionToken!,
                        Language = request.Language
                    },
                    token),
                settings.TimeoutMilliseconds,
                cancellationToken)
            : PlaceSearchResult<PlaceDetail>.Failed(PlaceSearchOutcome.RateLimited, _provider.Key);

        return Result.Success(new PlaceDetailsResponse
        {
            Status = answer.Outcome.ToString(),
            Provider = answer.ProviderKey,
            Place = answer.Value is { } place
                ? new PlaceDetailResponse
                {
                    PlaceId = place.PlaceId,
                    Latitude = place.Coordinate.Latitude,
                    Longitude = place.Coordinate.Longitude,
                    Name = place.Name,
                    FormattedAddress = place.FormattedAddress,
                    CountryCode = place.CountryCode,
                    Locality = place.Locality,
                    PostalCode = place.PostalCode
                }
                : null,
            Attribution = answer.IsSuccess ? _provider.Attribution : null,
            Message = PlaceSearchRules.Explain(answer.Outcome)
        });
    }
}
