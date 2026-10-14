using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Contracts.V1.Requests;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.Modules.Housing.Extensibility.Ranking;
using Diaspora.Modules.Housing.Extensibility.Search;
using Diaspora.Modules.Housing.Observability;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Application.Discovery;

/// <summary>
/// Runs a housing search.
/// <para>
/// <b>The handler translates and delegates; it does not query.</b> A client's loose parameters
/// become a <c>HousingListingQueryFilter</c> exactly once, here, and the search itself goes through
/// <c>IHousingSearchEngine</c>. That is what makes the brief's promise real: an OpenSearch adapter
/// replaces the engine and this file does not change.
/// </para>
/// <para>
/// <b>The audience fields are applied before the engine sees anything.</b> An engine — including a
/// future external one — receives a filter that already says who is asking, so a search index
/// cannot become the one place drafts and hidden listings leak.
/// </para>
/// <para>
/// <b>Ranking runs after the engine, over the page.</b> The engine orders by whatever it is good
/// at; a ranking strategy reorders the twenty rows actually being returned. Anything that needed
/// the whole result set could not be applied to a cursor page at all.
/// </para>
/// </summary>
public sealed class SearchHousingQueryHandler
    : IQueryHandler<SearchHousingQuery, HousingSearchResultResponse>
{
    private readonly IHousingSearchEngine _engine;
    private readonly HousingGeographicScopeAdapter _geography;
    private readonly HousingAuthorizationService _authorization;
    private readonly HousingListingReadContext _read;
    private readonly HousingRankingStrategyRegistry _ranking;
    private readonly HousingSearchFacetMapper _facets;
    private readonly IClock _clock;

    public SearchHousingQueryHandler(
        IHousingSearchEngine engine,
        HousingGeographicScopeAdapter geography,
        HousingAuthorizationService authorization,
        HousingListingReadContext read,
        HousingRankingStrategyRegistry ranking,
        HousingSearchFacetMapper facets,
        IClock clock)
    {
        _engine = engine;
        _geography = geography;
        _authorization = authorization;
        _read = read;
        _ranking = ranking;
        _facets = facets;
        _clock = clock;
    }

    public async Task<Result<HousingSearchResultResponse>> Handle(
        SearchHousingQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var request = query.Request;

        var scope = await _geography.ResolveAsync(
            request.City,
            request.CountryCode,
            request.AreaId,
            request.Latitude,
            request.Longitude,
            request.RadiusKm,
            request.Bounds,
            _authorization.CallerId,
            cancellationToken);

        if (scope.IsFailure)
        {
            return Result.Failure<HousingSearchResultResponse>(scope.Error);
        }

        // A point the member sent, as opposed to the centre the resolver gives a town search.
        var aroundPoint = request.Latitude is not null
            && request.Longitude is not null
            && string.IsNullOrWhiteSpace(request.Bounds);

        var filter = HousingGeographicScopeAdapter.Apply(
            BuildFilter(request), scope.Value, aroundPoint);

        // The audience half, from who is asking. Before the engine sees anything.
        filter = await _authorization.ApplyAudienceAsync(
            filter, includeOwn: false, cancellationToken);

        // The circle as Location resolved it — its centre and its radius — so "nearest first" is
        // measured from the same point, and cut at the same edge, as the distances on the cards.
        var circle = aroundPoint
            && scope.Value is { Center: { } centre, IsViewport: false } resolved
            && resolved.Radius.Scope == Location.Domain.Enums.RadiusScope.Radius
                ? (Latitude: centre.Latitude, Longitude: centre.Longitude, Kilometres: resolved.Radius.Distance.Kilometers)
                : ((double Latitude, double Longitude, double Kilometres)?)null;

        var outcome = await _engine.SearchAsync(
            new HousingSearchRequest
            {
                Filter = filter,
                Cursor = request.Cursor,
                IncludeFacets = request.IncludeFacets ?? false,
                Latitude = circle?.Latitude,
                Longitude = circle?.Longitude,
                RadiusKm = circle?.Kilometres
            },
            cancellationToken);

        var distances = _geography.MeasureDistances(scope.Value, outcome.Items, aroundPoint);

        var inside = HousingGeographicScopeAdapter.WithinRadius(
            scope.Value, outcome.Items, distances, aroundPoint);

        var appliedSort = outcome.AppliedSort ?? filter.Sort;

        // A ranking strategy reorders the page, which is only right when nobody asked for an order:
        // re-ranking a nearest-first, highest-rated or price page would put page two's order in
        // conflict with page one's.
        var ranked = appliedSort is HousingSortOrder.Newest or HousingSortOrder.Relevance
            ? _ranking.Rank(
                request.Ranking,
                inside,
                new HousingRankingContext
                {
                    ViewerId = _authorization.CallerId,
                    Now = _clock.UtcNow,
                    Latitude = request.Latitude,
                    Longitude = request.Longitude,
                    NeighborhoodAreaId = filter.NeighborhoodAreaId,
                    AvailableOnly = filter.AvailableOnly
                })
            : inside;

        HousingTelemetry.SearchesRun.Add(
            1,
            new KeyValuePair<string, object?>(
                "term", !string.IsNullOrWhiteSpace(request.Query)),
            new KeyValuePair<string, object?>("engine", outcome.EngineName ?? _engine.Name));

        return Result.Success(new HousingSearchResultResponse
        {
            Items = await _read.ToCardsAsync(ranked, distances, cancellationToken),
            NextCursor = outcome.NextCursor,
            HasMore = outcome.HasMore,
            TotalCount = outcome.TotalCount,
            Facets = outcome.Facets is null
                ? null
                : await _facets.ToResponseAsync(outcome.Facets, cancellationToken),
            ResolvedLocation = HousingGeographicScopeAdapter.Describe(scope.Value),
            Engine = outcome.EngineName ?? _engine.Name,
            AppliedSort = appliedSort.ToString()
        });
    }

    /// <summary>
    /// Translates the request into the module's own vocabulary.
    /// <para>
    /// The one place a client's strings meet the domain. Unparseable enum values are dropped rather
    /// than refused: a stale client sending a lease type that has been renamed should get results,
    /// not an error on a browse screen.
    /// </para>
    /// </summary>
    private static HousingListingQueryFilter BuildFilter(SearchHousingRequest request) => new()
    {
        Text = request.Query,
        CountryCode = request.CountryCode,
        CityName = request.City,
        // Accepted from old app builds and ignored (14 Oct 2026): a catalogue node narrows no
        // member's search. The neighbourhood pages keep their own routes until Phase 2.
        NeighborhoodAreaId = null,

        AccommodationTypeKeys = request.AccommodationTypes,
        LeaseTypes = ParseMany<LeaseType>(request.LeaseTypes),
        AmenityKeys = request.Amenities,
        MinimumFurnishedStatus = Parse<FurnishedStatus>(request.MinFurnishedStatus),
        FurnishedStatuses = ParseMany<FurnishedStatus>(request.FurnishedStatuses),

        MinimumBedrooms = request.MinBedrooms,
        MaximumBedrooms = request.MaxBedrooms,
        MinimumBathrooms = request.MinBathrooms,
        MinimumOccupants = request.MinOccupants,

        Currency = request.Currency,
        Period = Parse<RentPeriod>(request.RentPeriod),
        MinimumPrice = request.MinRent,
        MaximumPrice = request.MaxRent,
        UtilitiesIncludedOnly = request.UtilitiesIncluded ?? false,
        InternetIncludedOnly = request.InternetIncluded ?? false,
        NoDepositOnly = request.NoDeposit ?? false,

        StudentFriendlyOnly = request.StudentFriendly ?? false,
        FamilyFriendlyOnly = request.FamilyFriendly ?? false,
        ShortStayOnly = request.ShortStay ?? false,
        LongStayOnly = request.LongStay ?? false,
        SharedOnly = request.Shared ?? false,
        RoommateWantedOnly = request.RoommateWanted ?? false,
        PetsAllowedOnly = request.PetsAllowed ?? false,
        ParkingOnly = request.Parking ?? false,
        AccessibleOnly = request.Accessible ?? false,

        AvailableBy = request.AvailableBy,

        // Available-only defaults to true. Somebody searching for housing wants somewhere they can
        // actually take; a page full of reserved and occupied properties is a page of dead ends.
        AvailableOnly = request.AvailableOnly ?? true,

        VerifiedLandlordsOnly = request.VerifiedOnly ?? false,
        LandlordKinds = ParseMany<LandlordKind>(request.LandlordKinds),

        // An empty guid is the client sending "no landlord" the long way round, and it must not
        // become a filter that matches nothing.
        LandlordId = request.LandlordId is { } landlord && landlord != Guid.Empty
            ? LandlordProfileId.From(landlord)
            : null,

        Sort = Parse<HousingSortOrder>(request.Sort) ?? HousingSortOrder.Newest,
        Limit = request.Limit ?? HousingLimits.DefaultPageSize
    };

    private static TEnum? Parse<TEnum>(string? value) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : null;

    private static List<TEnum>? ParseMany<TEnum>(IReadOnlyList<string>? values)
        where TEnum : struct, Enum
    {
        if (values is not { Count: > 0 })
        {
            return null;
        }

        var parsed = values
            .Select(value => Enum.TryParse<TEnum>(value, ignoreCase: true, out var item)
                ? item
                : (TEnum?)null)
            .OfType<TEnum>()
            .Distinct()
            .ToList();

        return parsed.Count == 0 ? null : parsed;
    }
}
