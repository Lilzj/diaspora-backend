using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Financial.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Advertising;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Financial.Application.Consumers.Advertising;

/// <summary>
/// The purchase behind a campaign captured, so the campaign goes to review — MON-GAP-001.
/// <para>
/// <b>Without this the state machine has an unreachable half.</b> Submission leaves a campaign in
/// <c>PendingPayment</c>, and nothing else in the platform moves it out — so every campaign ever
/// submitted would sit waiting for a payment that had already cleared, and no reviewer would ever
/// see one. It is the same shape as a recorder nobody calls: the code exists, the tests pass, and
/// the feature does not work.
/// </para>
/// <para>
/// <b>The event is the purchase completing, not the payment authorising.</b> An authorisation is a
/// hold; a capture is money. A campaign that went to review on an authorisation could be approved
/// and start delivering against funds that never arrived.
/// </para>
/// <para>
/// <b>A purchase with no campaign is the ordinary case and passes in silence.</b> Every other
/// monetization product on the platform goes through this same event — a boost, a subscription, a
/// credit bundle — and none of them has a campaign above it.
/// </para>
/// </summary>
public sealed class AdCampaignFundedConsumer
    : IntegrationEventHandler<MonetizationProductPurchasedIntegrationEvent>
{
    private readonly IAdCampaignRepository _campaigns;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AdCampaignFundedConsumer> _logger;

    public AdCampaignFundedConsumer(
        IAdCampaignRepository campaigns,
        IUnitOfWork unitOfWork,
        ILogger<AdCampaignFundedConsumer> logger)
    {
        _campaigns = campaigns;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    protected override async Task HandleAsync(
        MonetizationProductPurchasedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // Found by what the purchase was *for*, which is how every other subject on this platform
        // is resolved. The campaign never had to store a purchase id it did not yet have.
        if (!string.Equals(integrationEvent.SubjectType, "Campaign", StringComparison.Ordinal))
        {
            return;
        }

        var campaign = await _campaigns.FindAsync(
            AdCampaignId.From(integrationEvent.SubjectId), cancellationToken);

        if (campaign is null)
        {
            return;
        }

        var funded = campaign.MarkFunded(
            MonetizationPurchaseId.From(integrationEvent.PurchaseId));

        if (funded.IsFailure)
        {
            // Already funded, cancelled while the payment was in flight, or otherwise past this
            // point. Logged rather than thrown: the money is real either way and a retry loop on
            // the dispatcher would not make the campaign submittable.
            _logger.LogWarning(
                "Purchase {PurchaseId} captured for campaign {CampaignId}, which is {Status} and "
                + "could not be funded: {Reason}",
                integrationEvent.PurchaseId,
                campaign.Id.Value,
                campaign.Status,
                funded.Error.Code);

            return;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Campaign {CampaignId} is funded and queued for review.", campaign.Id.Value);
    }
}
