using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Landlords;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.Reports;

namespace Diaspora.Modules.Housing.Application.Mapping;

/// <summary>
/// Turns reports into the moderation queue response.
/// <para>
/// <b>The reporter identity is included only because every path that reaches this mapper is already
/// behind the moderation permission.</b> There is no member-facing report response at all — not
/// even for a landlord reading reports against their own listing, because a landlord who could
/// enumerate them could work out who filed them.
/// </para>
/// </summary>
public static class HousingReportMapper
{
    public static HousingReportResponse ToResponse(
        HousingReport report,
        HousingListing? listing,
        LandlordProfile? landlord,
        int landlordUpheldReports)
    {
        ArgumentNullException.ThrowIfNull(report);

        return new HousingReportResponse
        {
            Id = report.Id.Value,
            ListingId = report.ListingId.Value,
            ListingTitle = listing?.Title.Value ?? string.Empty,
            LandlordId = report.LandlordId.Value,
            LandlordName = landlord?.Identity.DisplayName ?? string.Empty,
            Reason = report.Reason.ToString(),
            Detail = report.Detail,
            Status = report.Status.ToString(),
            ReportedBy = report.ReportedBy.Value,
            ResolvedBy = report.ResolvedBy?.Value,
            ResolvedAt = report.ResolvedAt,
            Resolution = report.Resolution,
            LandlordUpheldReports = landlordUpheldReports,
            CreatedAt = report.CreatedAt
        };
    }
}
