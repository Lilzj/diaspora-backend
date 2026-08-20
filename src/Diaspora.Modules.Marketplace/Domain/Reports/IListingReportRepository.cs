using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Marketplace.Domain.Reports;

/// <summary>How the application layer reaches listing reports.</summary>
public interface IListingReportRepository
{
    Task<ListingReport?> GetAsync(
        ListingReportId id, CancellationToken cancellationToken = default);

    /// <summary>This member's report on this listing, if they have already filed one.</summary>
    Task<ListingReport?> FindAsync(
        MarketplaceListingId listingId,
        UserId reporterId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// How many distinct members have reported a listing. The number that decides how urgently a
    /// moderator looks, and the one denormalised onto the listing itself.
    /// </summary>
    Task<int> CountForListingAsync(
        MarketplaceListingId listingId, CancellationToken cancellationToken = default);

    /// <summary>The moderation queue, newest first within a status.</summary>
    Task<CursorPagedResult<ListingReport>> QueryAsync(
        ListingReportStatus? status,
        ListingReportReason? reason,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Every open report on one listing. What resolving the listing closes in a batch.</summary>
    Task<IReadOnlyList<ListingReport>> GetOpenForListingAsync(
        MarketplaceListingId listingId, CancellationToken cancellationToken = default);

    Task AddAsync(ListingReport report, CancellationToken cancellationToken = default);
}
