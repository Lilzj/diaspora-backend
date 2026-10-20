using Diaspora.Modules.Financial.Domain.Advertising;
using Diaspora.Modules.Financial.Domain.Catalog;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Placements;
using Diaspora.Modules.Financial.Domain.Purchases;
using Diaspora.Modules.Financial.Extensibility.Sponsorship;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Modules.Financial.Application.Sponsorship;

/// <summary>
/// B15's answer to <see cref="ISponsoredContentDirectory"/>.
/// <para>
/// <b>Two sources, never merged into one row.</b> A catalogue purchase creates a
/// <see cref="Placement"/>; an advertising campaign never does (nothing calls
/// <c>AdCampaign.AddPlacement</c>), so the two cannot describe the same promotion twice.
/// </para>
/// <para>
/// <b>Money is what was captured, not what was asked.</b> <c>AmountPaid</c> comes from the purchase
/// and only once it completed. A placement whose purchase is still pending reads
/// <c>PendingPayment</c> whatever the placement row says, because the row is written before the
/// money arrives.
/// </para>
/// </summary>
public sealed class SponsoredContentDirectory : ISponsoredContentDirectory
{
    private readonly IPlacementRepository _placements;
    private readonly IAdCampaignRepository _campaigns;
    private readonly IMonetizationPurchaseRepository _purchases;
    private readonly IMonetizationProductRepository _products;
    private readonly IAdPerformanceRepository _performance;
    private readonly IClock _clock;

    public SponsoredContentDirectory(
        IPlacementRepository placements,
        IAdCampaignRepository campaigns,
        IMonetizationPurchaseRepository purchases,
        IMonetizationProductRepository products,
        IAdPerformanceRepository performance,
        IClock clock)
    {
        _placements = placements;
        _campaigns = campaigns;
        _purchases = purchases;
        _products = products;
        _performance = performance;
        _clock = clock;
    }

    public async Task<IReadOnlyList<SponsoredContentSnapshot>> ListForMemberAsync(
        Guid memberUserId, int take, CancellationToken cancellationToken = default)
    {
        var bounded = Math.Clamp(take, 1, 100);
        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        var products = new Dictionary<string, MonetizationProduct?>(StringComparer.OrdinalIgnoreCase);
        var result = new List<SponsoredContentSnapshot>();

        var placements = await _placements.ListByPurchaserAsync(
            memberUserId, status: null, before: null, excludeIds: [], bounded, cancellationToken);

        foreach (var placement in placements)
        {
            var purchase = placement.PurchaseId is null
                ? null
                : await _purchases.FindAsync(placement.PurchaseId, cancellationToken);

            var product = await ProductAsync(placement.ProductCode, products, cancellationToken);

            var state = purchase?.Status switch
            {
                PurchaseStatus.Pending => "PendingPayment",
                PurchaseStatus.Failed or PurchaseStatus.Cancelled => "Cancelled",
                PurchaseStatus.Refunded => "Refunded",
                _ => PlacementState(placement.Status)
            };

            var renewable = product is { IsRenewable: true }
                && placement.Status is not (PlacementStatus.Cancelled or PlacementStatus.Refunded)
                && purchase?.Status is null or PurchaseStatus.Completed;

            var rows = await _performance.ListForPlacementAsync(
                placement.Id,
                DateOnly.FromDateTime(placement.Window.StartsAt.UtcDateTime),
                today,
                cancellationToken);

            result.Add(new SponsoredContentSnapshot(
                PlacementId: placement.Id.Value,
                CampaignId: null,
                SubjectType: placement.Subject.SubjectType.ToString(),
                SubjectId: placement.Subject.SubjectId,
                ProductCode: placement.ProductCode,
                PackageLabel: product?.Name,
                State: state,
                RejectionReason: null,
                AmountPaid: Paid(purchase),
                StartsAt: placement.Window.StartsAt,
                EndsAt: placement.Window.EndsAt,
                IsRenewable: renewable,
                CampaignTitle: null,
                Performance: Sum(rows),
                CreatedAt: placement.CreatedAt));
        }

        var campaigns = await _campaigns.ListForOwnerAsync(
            memberUserId, statuses: null, bounded, cancellationToken);

        foreach (var campaign in campaigns)
        {
            // A campaign that promotes nothing in particular is advertising, not sponsored content.
            if (campaign.Subject is null)
            {
                continue;
            }

            var purchase = campaign.PurchaseId is null
                ? null
                : await _purchases.FindAsync(campaign.PurchaseId, cancellationToken);

            var product = campaign.ProductCode is null
                ? null
                : await ProductAsync(campaign.ProductCode, products, cancellationToken);

            var rows = await _performance.ListForCampaignAsync(
                campaign.Id,
                DateOnly.FromDateTime(campaign.Schedule.StartsAt.UtcDateTime),
                today,
                cancellationToken);

            result.Add(new SponsoredContentSnapshot(
                PlacementId: null,
                CampaignId: campaign.Id.Value,
                SubjectType: campaign.Subject.SubjectType.ToString(),
                SubjectId: campaign.Subject.SubjectId,
                ProductCode: campaign.ProductCode,
                PackageLabel: product?.Name,
                State: CampaignState(campaign.Status),
                RejectionReason: campaign.Status == AdCampaignStatus.Rejected ? campaign.ReviewNote : null,
                AmountPaid: Paid(purchase),
                StartsAt: campaign.Schedule.StartsAt,
                EndsAt: campaign.Schedule.EndsAt,

                // A campaign runs once. Running it again is a new campaign, not a renewal.
                IsRenewable: false,
                CampaignTitle: string.IsNullOrWhiteSpace(campaign.Headline) ? campaign.Name : campaign.Headline,
                Performance: Sum(rows),
                CreatedAt: campaign.CreatedAt));
        }

        return result.OrderByDescending(item => item.CreatedAt).ToList();
    }

    private async Task<MonetizationProduct?> ProductAsync(
        string code,
        Dictionary<string, MonetizationProduct?> cache,
        CancellationToken cancellationToken)
    {
        if (!cache.TryGetValue(code, out var product))
        {
            product = await _products.FindByCodeAsync(code, cancellationToken);
            cache[code] = product;
        }

        return product;
    }

    private static SponsoredAmount? Paid(MonetizationPurchase? purchase) =>
        purchase is { Status: PurchaseStatus.Completed }
            ? new SponsoredAmount(purchase.PaidMinorUnits, purchase.Currency, purchase.Exponent)
            : null;

    private static SponsoredPerformance? Sum(IReadOnlyList<AdPerformanceDay> rows) =>
        rows.Count == 0
            ? null
            : new SponsoredPerformance(
                rows.Sum(row => row.Impressions),
                rows.Sum(row => row.Clicks),
                rows.Sum(row => row.Conversions));

    private static string PlacementState(PlacementStatus status) => status switch
    {
        PlacementStatus.Scheduled => "Scheduled",
        PlacementStatus.Active => "Active",
        PlacementStatus.Paused => "Paused",
        PlacementStatus.Expired => "Expired",
        PlacementStatus.Cancelled => "Cancelled",
        PlacementStatus.Refunded => "Refunded",
        _ => status.ToString()
    };

    private static string CampaignState(AdCampaignStatus status) => status switch
    {
        AdCampaignStatus.Draft => "Draft",
        AdCampaignStatus.PendingPayment => "PendingPayment",

        // Submitted is "paid for, waiting for a reviewer to pick it up" — to the creator that is
        // the same wait as being under review.
        AdCampaignStatus.Submitted or AdCampaignStatus.UnderReview => "PendingApproval",
        AdCampaignStatus.Approved => "Approved",
        AdCampaignStatus.Scheduled => "Scheduled",
        AdCampaignStatus.Active => "Active",
        AdCampaignStatus.Paused => "Paused",
        AdCampaignStatus.Completed => "Completed",
        AdCampaignStatus.Rejected => "Rejected",
        AdCampaignStatus.Cancelled => "Cancelled",
        _ => status.ToString()
    };
}
