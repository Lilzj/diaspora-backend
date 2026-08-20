using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Domain.Events;
using Diaspora.Modules.Financial.Domain.Giving;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Placements;
using Diaspora.Modules.Financial.Domain.Purchases;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Modules.Financial.Application.EventHandlers;

/// <summary>
/// Everything that happens when money actually arrives.
/// <para>
/// <b>One handler, doing two jobs, and that is deliberate rather than untidy.</b> The platform's
/// <c>DSP1002</c> rule refuses two handlers settling one domain event in one module, for a reason
/// worth restating: two subscriptions on one fact run in an order nobody declared, and the first one
/// to throw silently cancels the second. Capture is the single most consequential fact this module
/// produces, so it gets one handler and an explicit order.
/// </para>
/// <para>
/// <b>The order matters and is stated here rather than being emergent.</b> Downstream state is
/// settled first — the purchase completes, the placement activates, the donation is receipted, the
/// fund's totals move — and only then is the capture announced. A consumer told about a capture
/// before the placement it paid for exists would read a subject with nothing in force over it.
/// </para>
/// <para>
/// <b>This runs inside the capturing transaction.</b> A domain event handler is inline and may read
/// uncommitted state from the same unit of work, which is exactly what makes "the placement is
/// active by the time anybody is told" true rather than hopeful.
/// </para>
/// </summary>
public sealed class PaymentCapturedEventHandler : DomainEventHandler<PaymentCapturedDomainEvent>
{
    private readonly IMonetizationPurchaseRepository _purchases;
    private readonly IPlacementRepository _placements;
    private readonly IDonationRepository _donations;
    private readonly IDonationCauseRepository _causes;
    private readonly ICampaignRepository _campaigns;
    private readonly ICommunityContributionRepository _contributions;
    private readonly IContributionFundRepository _funds;
    private readonly FinancialIntegrationEventRelay _relay;
    private readonly Services.FinancialIdentityService _identities;
    private readonly IClock _clock;

    public PaymentCapturedEventHandler(
        IMonetizationPurchaseRepository purchases,
        IPlacementRepository placements,
        IDonationRepository donations,
        IDonationCauseRepository causes,
        ICampaignRepository campaigns,
        ICommunityContributionRepository contributions,
        IContributionFundRepository funds,
        FinancialIntegrationEventRelay relay,
        Services.FinancialIdentityService identities,
        IClock clock)
    {
        _purchases = purchases;
        _placements = placements;
        _donations = donations;
        _causes = causes;
        _campaigns = campaigns;
        _contributions = contributions;
        _funds = funds;
        _relay = relay;
        _identities = identities;
        _clock = clock;
    }

    protected override async Task HandleAsync(
        PaymentCapturedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var intentId = domainEvent.PaymentIntentId;
        var now = _clock.UtcNow;

        await SettlePurchaseAsync(intentId, now, cancellationToken);
        await SettleDonationAsync(intentId, now, cancellationToken);
        await SettleContributionAsync(intentId, now, cancellationToken);

        await _relay.PublishAsync(domainEvent, cancellationToken);
        await AnnounceDepositAsync(domainEvent, cancellationToken);
    }

    /// <summary>
    /// Announces a top-up as well as the capture behind it.
    /// <para>
    /// Both facts are true and a consumer wanting one should not have to infer it from a subject type
    /// it has no business reading. B13 sends a top-up receipt off the deposit event and a payment
    /// receipt off the capture — and sending the wrong one tells a member they have been charged.
    /// </para>
    /// </summary>
    private async Task AnnounceDepositAsync(
        PaymentCapturedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        if (domainEvent.Purpose != Domain.Enums.PaymentPurpose.Deposit)
        {
            return;
        }

        var wallet = await _identities.PrimaryWalletAsync(
            domainEvent.AccountId, domainEvent.Currency, cancellationToken);

        if (wallet is null)
        {
            return;
        }

        await _relay.PublishDepositAsync(
            domainEvent, wallet.Id.Value, domainEvent.MethodKind.ToString(), cancellationToken);
    }

    /// <summary>
    /// Completes a purchase and activates whatever it granted.
    /// <para>
    /// The placement was created scheduled at purchase time precisely so that this — and only this —
    /// puts it in force. A placement activated at purchase is one somebody keeps when the payment
    /// later fails.
    /// </para>
    /// </summary>
    private async Task SettlePurchaseAsync(
        PaymentIntentId intentId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var purchase = await _purchases.FindByPaymentIntentAsync(intentId, cancellationToken);

        if (purchase is null || purchase.Status == Domain.Enums.PurchaseStatus.Completed)
        {
            return;
        }

        purchase.Complete(now);
        _purchases.Update(purchase);

        if (purchase.PlacementId is not { } placementId)
        {
            return;
        }

        var placement = await _placements.FindAsync(placementId, cancellationToken);

        if (placement is null)
        {
            return;
        }

        // A renewal extends the window that exists; a first purchase activates a new one. The two
        // are told apart by a field on the purchase rather than by inspecting the placement's state,
        // because a placement that has already lapsed is a perfectly ordinary thing to renew.
        if (purchase.RenewalDays is { } days)
        {
            placement.Extend(days, now);
        }
        else
        {
            placement.Activate(now);
        }

        _placements.Update(placement);
    }

    private async Task SettleDonationAsync(
        PaymentIntentId intentId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var donation = await _donations.FindByPaymentIntentAsync(intentId, cancellationToken);

        if (donation is null || donation.Status == Domain.Enums.GivingStatus.Completed)
        {
            return;
        }

        // The donation's own transaction is the capture's. Recording it here is what lets a support
        // agent walk from a receipt reference to the ledger entries and back.
        donation.Complete(TransactionId.From(intentId.Value), now);
        _donations.Update(donation);

        if (donation.CauseId is not { } causeId)
        {
            return;
        }

        var cause = await _causes.FindAsync(causeId, cancellationToken);

        if (cause is null)
        {
            return;
        }

        cause.RecordDonation(donation.Amount, now);
        _causes.Update(cause);

        await EvaluateMilestonesAsync(cause, now, cancellationToken);
    }

    /// <summary>
    /// Stamps any campaign milestone this donation has just carried the cause past.
    /// <para>
    /// <b>Inside this handler rather than in a second one, and the rule is the platform's.</b>
    /// <c>DSP1002</c> refuses two handlers settling one domain event in one module: two subscriptions
    /// on one fact run in an order nobody declared, and the first to throw silently cancels the
    /// second. Milestones are read straight off the cause's new total, which is why this runs after
    /// <c>RecordDonation</c> and not before.
    /// </para>
    /// <para>
    /// <b>Only campaigns have milestones, and most causes are not campaigns.</b> A cause created by
    /// staff has no campaign behind it and the lookup returns nothing, which costs one indexed read
    /// on a code that is uniquely indexed.
    /// </para>
    /// </summary>
    private async Task EvaluateMilestonesAsync(
        DonationCause cause, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var campaign = await _campaigns.FindByCauseCodeAsync(cause.Code, cancellationToken);

        if (campaign is null || campaign.Milestones.Count == 0)
        {
            return;
        }

        if (campaign.EvaluateMilestones(cause.RaisedMinorUnits, now).Count > 0)
        {
            _campaigns.Update(campaign);
        }
    }

    private async Task SettleContributionAsync(
        PaymentIntentId intentId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var contribution = await _contributions.FindByPaymentIntentAsync(intentId, cancellationToken);

        if (contribution is null || contribution.Status == Domain.Enums.GivingStatus.Completed)
        {
            return;
        }

        contribution.Complete(now, TransactionId.From(intentId.Value));
        _contributions.Update(contribution);

        var fund = await _funds.FindAsync(contribution.FundId, cancellationToken);

        if (fund is null)
        {
            return;
        }

        fund.RecordContribution(contribution.Amount, now);
        _funds.Update(fund);
    }
}
