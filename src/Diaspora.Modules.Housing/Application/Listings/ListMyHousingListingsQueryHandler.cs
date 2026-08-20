using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Application.Listings;

/// <summary>
/// The caller's own properties.
/// <para>
/// Uses <c>FindCallerLandlordAsync</c> rather than <c>RequireLandlordAsync</c>, deliberately: a
/// suspended landlord must still be able to see their properties and read why they were suspended.
/// A screen that went blank on suspension would leave them unable to correct whatever caused it.
/// </para>
/// </summary>
public sealed class ListMyHousingListingsQueryHandler
    : IQueryHandler<ListMyHousingListingsQuery, HousingSearchResultResponse>
{
    private readonly IHousingListingRepository _listings;
    private readonly HousingAuthorizationService _authorization;
    private readonly HousingListingReadContext _read;

    public ListMyHousingListingsQueryHandler(
        IHousingListingRepository listings,
        HousingAuthorizationService authorization,
        HousingListingReadContext read)
    {
        _listings = listings;
        _authorization = authorization;
        _read = read;
    }

    public async Task<Result<HousingSearchResultResponse>> Handle(
        ListMyHousingListingsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var landlord = await _authorization.FindCallerLandlordAsync(cancellationToken);

        if (landlord is null)
        {
            return Result.Failure<HousingSearchResultResponse>(HousingErrors.NotLandlord);
        }

        var filter = new HousingListingQueryFilter
        {
            LandlordId = landlord.Id,
            ViewerId = _authorization.CallerId,
            ViewerLandlordId = landlord.Id,
            ViewerIsStaff = _authorization.IsStaff,
            IncludeNonPublic = true,
            Statuses = Enum.TryParse<HousingListingStatus>(
                request.Status, ignoreCase: true, out var status)
                ? [status]
                : null,
            Sort = HousingSortOrder.Newest,
            Limit = request.Limit ?? Domain.Policies.HousingLimits.DefaultPageSize
        };

        var page = await _listings.QueryAsync(
            filter, request.Cursor, filter.EffectiveLimit, cancellationToken);

        var cards = await _read.ToCardsAsync(page.Items, cancellationToken);

        return Result.Success(new HousingSearchResultResponse
        {
            Items = cards,
            NextCursor = page.NextCursor,
            HasMore = page.HasMore,
            TotalCount = await _listings.CountAsync(filter, cancellationToken)
        });
    }
}
