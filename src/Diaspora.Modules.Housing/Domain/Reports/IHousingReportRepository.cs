using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Housing.Domain.Reports;

/// <summary>How the module reaches its housing reports. Every path is behind moderation.</summary>
public interface IHousingReportRepository
{
    Task<HousingReport?> GetAsync(
        HousingReportId id, CancellationToken cancellationToken = default);

    Task<CursorPagedResult<HousingReport>> QueryAsync(
        HousingReportQueryFilter filter,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Whether this member has already reported this listing. One report each.</summary>
    Task<bool> HasReportedAsync(
        HousingListingId listingId,
        UserId reportedBy,
        CancellationToken cancellationToken = default);

    /// <summary>Distinct members who have reported it. What the auto-flag threshold compares.</summary>
    Task<int> CountForListingAsync(
        HousingListingId listingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upheld reports against a landlord. Fed to the fraud seam: a landlord with three upheld scam
    /// reports is a different proposition from one with three dismissed ones.
    /// </summary>
    Task<int> CountUpheldForLandlordAsync(
        LandlordProfileId landlordId, CancellationToken cancellationToken = default);

    Task AddAsync(HousingReport report, CancellationToken cancellationToken = default);
}
