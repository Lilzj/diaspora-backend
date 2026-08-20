using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Application.Mapping;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Landlords;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.Modules.Housing.Domain.Reports;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Application.Moderation;

/// <summary>
/// The housing moderation queue.
/// <para>
/// <b>The upheld-report count travels with every row, and it is the queue's most useful column.</b>
/// A moderator deciding on one report needs to know whether this landlord has three upheld ones
/// already — that is the difference between a mistake and a pattern, and looking it up per row by
/// hand is what a busy queue stops doing.
/// </para>
/// </summary>
public sealed class ListHousingReportsQueryHandler
    : IQueryHandler<ListHousingReportsQuery, IReadOnlyList<HousingReportResponse>>
{
    private readonly IHousingReportRepository _reports;
    private readonly IHousingListingRepository _listings;
    private readonly ILandlordProfileRepository _landlords;

    public ListHousingReportsQueryHandler(
        IHousingReportRepository reports,
        IHousingListingRepository listings,
        ILandlordProfileRepository landlords)
    {
        _reports = reports;
        _listings = listings;
        _landlords = landlords;
    }

    public async Task<Result<IReadOnlyList<HousingReportResponse>>> Handle(
        ListHousingReportsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var filter = new HousingReportQueryFilter
        {
            ListingId = request.ListingId is { } listingId && listingId != Guid.Empty
                ? HousingListingId.From(listingId)
                : null,
            OpenOnly = request.OpenOnly,
            ScamsFirst = request.ScamsFirst,
            Limit = request.Limit ?? HousingLimits.DefaultPageSize
        };

        var page = await _reports.QueryAsync(
            filter, request.Cursor, filter.EffectiveLimit, cancellationToken);

        if (page.Items.Count == 0)
        {
            return Result.Success<IReadOnlyList<HousingReportResponse>>([]);
        }

        var listings = (await _listings.GetManyAsync(
                [.. page.Items.Select(report => report.ListingId).Distinct()],
                cancellationToken))
            .ToDictionary(listing => listing.Id);

        var landlordIds = page.Items
            .Select(report => report.LandlordId)
            .Distinct()
            .ToList();

        var landlords = await _landlords.GetManyAsync(landlordIds, cancellationToken);

        // One count per landlord rather than one per report: a queue page routinely holds several
        // reports against the same landlord.
        var upheld = new Dictionary<LandlordProfileId, int>();

        foreach (var landlordId in landlordIds)
        {
            upheld[landlordId] = await _reports.CountUpheldForLandlordAsync(
                landlordId, cancellationToken);
        }

        return Result.Success<IReadOnlyList<HousingReportResponse>>(
        [
            .. page.Items.Select(report => HousingReportMapper.ToResponse(
                report,
                listings.GetValueOrDefault(report.ListingId),
                landlords.GetValueOrDefault(report.LandlordId),
                upheld.GetValueOrDefault(report.LandlordId)))
        ]);
    }
}
