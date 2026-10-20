using Diaspora.Contracts.Financial.V1.Giving;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Giving;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Financial.Application.Campaigns;

/// <summary>
/// A fundraising campaign's giving, contributed to the Analytics module.
/// <para>
/// <b>This module answers because it owns the campaign, its cause, every gift and the rule for who
/// may run a campaign.</b> The rule is <see cref="CampaignAccessService.CanManageAsync"/> — the
/// organiser of a personal campaign, anybody holding the body's campaign capability for an
/// organisation's — plus staff, applied here rather than restated in Analytics so the two cannot
/// drift.
/// </para>
/// <para>
/// Read-only, and no donor identifier or message leaves this class.
/// </para>
/// </summary>
public sealed class CampaignAnalyticsSource : ICampaignAnalyticsSource
{
    private readonly ICampaignRepository _campaigns;
    private readonly IDonationCauseRepository _causes;
    private readonly IDonationRepository _donations;
    private readonly CampaignAccessService _access;
    private readonly FinancialAuthorizationService _authorization;
    private readonly CurrencyCatalog _currencies;
    private readonly DiasporaDbContext _dbContext;

    public CampaignAnalyticsSource(
        ICampaignRepository campaigns,
        IDonationCauseRepository causes,
        IDonationRepository donations,
        CampaignAccessService access,
        FinancialAuthorizationService authorization,
        CurrencyCatalog currencies,
        DiasporaDbContext dbContext)
    {
        _campaigns = campaigns;
        _causes = causes;
        _donations = donations;
        _access = access;
        _authorization = authorization;
        _currencies = currencies;
        _dbContext = dbContext;
    }

    public async Task<CampaignAnalyticsReading?> ReadAsync(
        Guid campaignId,
        Guid viewerUserId,
        bool viewerIsPlatformAnalyst,
        DateTimeOffset baselineFrom,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        if (campaignId == Guid.Empty)
        {
            return null;
        }

        var campaign = await _campaigns
            .FindAsync(CampaignId.From(campaignId), cancellationToken)
            .ConfigureAwait(false);

        if (campaign is null)
        {
            return null;
        }

        var mayRead = viewerIsPlatformAnalyst
            || _authorization.Has(FinancialPermissions.GivingManage)
            || await _access
                .CanManageAsync(campaign, viewerUserId == Guid.Empty ? null : viewerUserId, cancellationToken)
                .ConfigureAwait(false);

        if (!mayRead)
        {
            return new CampaignAnalyticsReading(
                campaign.Id.Value, campaign.Title, false, campaign.Status.ToString(),
                campaign.Currency, campaign.Exponent, null, null, false, 0,
                CampaignGivingWindow.Empty, CampaignGivingWindow.Empty, []);
        }

        var symbol = await _currencies.SymbolAsync(campaign.Currency, cancellationToken).ConfigureAwait(false);

        var cause = campaign.CauseId is { } causeId
            ? await _causes.FindAsync(causeId, cancellationToken).ConfigureAwait(false)
            : null;

        if (cause is null)
        {
            // A draft, or a campaign never published: no cause, so nothing can have been given.
            return new CampaignAnalyticsReading(
                campaign.Id.Value, campaign.Title, true, campaign.Status.ToString(),
                campaign.Currency, campaign.Exponent, symbol, campaign.TargetMinorUnits, false, 0,
                CampaignGivingWindow.Empty, CampaignGivingWindow.Empty, []);
        }

        // The repository's window is inclusive at both ends; the seam's is half-open.
        var current = await _donations
            .SummariseForCauseAsync(cause.Id, from, to.AddTicks(-1), cancellationToken)
            .ConfigureAwait(false);

        var baseline = await _donations
            .SummariseForCauseAsync(cause.Id, baselineFrom, from.AddTicks(-1), cancellationToken)
            .ConfigureAwait(false);

        var currency = cause.Currency;
        var donationCauseId = cause.Id;

        // Gifts that arrived in the period, by the instant the money arrived. A refunded gift is not
        // money the cause holds, so only completed gifts draw the series.
        var gifts = await _dbContext.Set<Donation>()
            .AsNoTracking()
            .Where(donation => donation.CauseId == donationCauseId
                && donation.Status == GivingStatus.Completed
                && donation.Currency == currency
                && donation.CompletedAt != null
                && donation.CompletedAt >= from
                && donation.CompletedAt < to)
            .Select(donation => new { Completed = donation.CompletedAt!.Value, donation.AmountMinorUnits })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new CampaignAnalyticsReading(
            campaign.Id.Value,
            campaign.Title,
            true,
            campaign.Status.ToString(),
            cause.Currency,
            cause.Exponent,
            symbol,
            campaign.TargetMinorUnits,
            true,
            cause.RaisedMinorUnits,
            Window(current),
            Window(baseline),
            [.. gifts.Select(gift => new CampaignGift(gift.Completed, gift.AmountMinorUnits))]);
    }

    private static CampaignGivingWindow Window(DonationWindowSummary summary) => new(
        summary.CompletedMinorUnits,
        summary.CompletedCount,
        summary.SupporterCount,
        summary.RefundedMinorUnits,
        summary.RefundedCount);
}
