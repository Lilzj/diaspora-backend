using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.Modules.Marketplace.Domain.Reports;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.SharedKernel.Pagination;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Moderation;

/// <summary>
/// Reads the moderation queue.
/// <para>
/// <b>The reporter's identity is included here and on no other surface.</b> Who reported a listing
/// is the most retaliation-prone fact this module holds; a moderator needs it to spot a member
/// filing fifty reports a day, and nobody else does.
/// </para>
/// </summary>
public sealed class ListModerationQueueQueryHandler
    : IQueryHandler<ListModerationQueueQuery, CursorPagedResult<ListingReportResponse>>
{
    private readonly IListingReportRepository _reports;
    private readonly IMarketplaceListingRepository _listings;
    private readonly ISellerProfileRepository _sellers;
    private readonly MarketplaceAuthorizationService _authorization;

    public ListModerationQueueQueryHandler(
        IListingReportRepository reports,
        IMarketplaceListingRepository listings,
        ISellerProfileRepository sellers,
        MarketplaceAuthorizationService authorization)
    {
        _reports = reports;
        _listings = listings;
        _sellers = sellers;
        _authorization = authorization;
    }

    public async Task<Result<CursorPagedResult<ListingReportResponse>>> Handle(
        ListModerationQueueQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!_authorization.IsStaff)
        {
            return Result.Failure<CursorPagedResult<ListingReportResponse>>(
                MarketplaceErrors.NotModerator);
        }

        var limit = Math.Clamp(
            query.Limit ?? MarketplaceLimits.DefaultPageSize, 1, MarketplaceLimits.MaxPageSize);

        var page = await _reports.QueryAsync(
            MarketplaceWireNames.ParseReportStatus(query.Status),
            MarketplaceWireNames.ParseReportReason(query.Reason),
            query.Cursor,
            limit,
            cancellationToken);

        var listings = await _listings.GetManyAsync(
            [.. page.Items.Select(report => report.ListingId).Distinct()], cancellationToken);

        var byId = listings.ToDictionary(listing => listing.Id);

        var sellers = await _sellers.GetManyAsync(
            [.. page.Items.Select(report => report.SellerId).Distinct()], cancellationToken);

        var items = new List<ListingReportResponse>(page.Items.Count);

        foreach (var report in page.Items)
        {
            var listing = byId.GetValueOrDefault(report.ListingId);

            items.Add(new ListingReportResponse
            {
                ReportId = report.Id.Value,
                ListingId = report.ListingId.Value,
                ListingTitle = listing?.Title.Value ?? string.Empty,
                SellerId = report.SellerId.Value,
                SellerName = sellers.GetValueOrDefault(report.SellerId)
                    ?.Identity.DisplayName ?? string.Empty,
                Reason = report.Reason.ToString(),
                Detail = report.Detail,
                Status = report.Status.ToString(),
                TotalReportsOnListing = listing?.ReportCount ?? 0,

                // Staff view only. See the class summary.
                ReporterId = report.ReporterId.Value,
                Resolution = report.Resolution,
                ResolvedAt = report.ResolvedAt,
                CreatedAt = report.CreatedAt
            });
        }

        return Result.Success(
            CursorPagedResult<ListingReportResponse>.Create(
                items, page.NextCursor, page.HasMore));
    }
}
