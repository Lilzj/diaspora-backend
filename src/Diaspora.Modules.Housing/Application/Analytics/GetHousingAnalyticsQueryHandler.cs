using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Application.Mapping;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Analytics;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Application.Analytics;

/// <summary>
/// Reads a property measured record.
/// <para>
/// Loads the listing including deleted rows, because a landlord's analytics outlive the listing
/// visibility: the point of the record is that it survives archival, and refusing to show it once
/// the property is gone would defeat the reason it is a separate aggregate.
/// </para>
/// </summary>
public sealed class GetHousingAnalyticsQueryHandler
    : IQueryHandler<GetHousingAnalyticsQuery, HousingAnalyticsResponse>
{
    private readonly IHousingListingRepository _listings;
    private readonly IHousingAnalyticsRepository _analytics;
    private readonly HousingAuthorizationService _authorization;

    public GetHousingAnalyticsQueryHandler(
        IHousingListingRepository listings,
        IHousingAnalyticsRepository analytics,
        HousingAuthorizationService authorization)
    {
        _listings = listings;
        _analytics = analytics;
        _authorization = authorization;
    }

    public async Task<Result<HousingAnalyticsResponse>> Handle(
        GetHousingAnalyticsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var listingId = HousingListingId.From(request.ListingId);

        var listing = await _listings.GetIncludingDeletedAsync(listingId, cancellationToken);

        if (listing is null)
        {
            return Result.Failure<HousingAnalyticsResponse>(HousingErrors.ListingNotFound);
        }

        // A read, so staff keep it: the owner check no longer lets them through (it answers "may
        // you change this"), and reading a property's figures changes nothing. Marketplace's
        // analytics read draws the same line.
        if (!_authorization.IsStaff)
        {
            var access = await _authorization.RequireListingAccessAsync(listing, cancellationToken);

            if (access.IsFailure)
            {
                return Result.Failure<HousingAnalyticsResponse>(access.Error);
            }
        }

        var analytics = await _analytics.GetByListingForReadAsync(
            listingId, cancellationToken);

        return analytics is null
            ? Result.Failure<HousingAnalyticsResponse>(HousingErrors.ListingNotFound)
            : Result.Success(HousingAnalyticsMapper.ToResponse(analytics));
    }
}
