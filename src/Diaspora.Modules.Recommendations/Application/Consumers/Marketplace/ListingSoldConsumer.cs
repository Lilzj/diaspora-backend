using Diaspora.Contracts.Marketplace.V1.Events;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.ValueObjects;
using Diaspora.Modules.Recommendations.Extensibility.Signals;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Recommendations.Application.Consumers.Marketplace;

/// <summary>
/// Something was sold.
/// <para>
/// <b>Two things happen, and only one of them is conditional.</b> The buyer's transaction signal is
/// the strongest positive the marketplace produces and is always recorded. The withdrawal only
/// happens when the listing actually sold out — a seller with ten of something who sold one still
/// has nine, and withdrawing on the first sale would empty the marketplace of everything popular.
/// </para>
/// <para>
/// <b>The buyer may be unknown, and that is normal rather than an error.</b> A sale recorded outside
/// the platform's own reservation flow has no buyer identifier; the withdrawal still applies, and
/// only the signal is skipped.
/// </para>
/// </summary>
public sealed class ListingSoldConsumer : RecommendationEventConsumer<ListingSoldIntegrationEvent>
{
    public ListingSoldConsumer(CandidateProjector projector, ISignalStore signals)
        : base(projector, signals) { }

    protected override SignalSource Source => SignalSource.Marketplace;

    protected override async Task HandleAsync(
        ListingSoldIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (integrationEvent.BuyerUserId is { } buyerId)
        {
            await RecordAsync(
                Signal(
                    UserId.From(buyerId),
                    SignalKind.Transacted,
                    integrationEvent.SoldAt,
                    RecommendationTargetType.MarketplaceListing,
                    integrationEvent.ListingId,
                    integrationEvent.CategoryKey,
                    InterestKind.Category,
                    SignalContext.Create(integrationEvent.CountryCode)),
                cancellationToken);
        }

        // Only when there is nothing left. See the class summary.
        if (integrationEvent.IsSoldOut)
        {
            await Projector.WithdrawAsync(
                RecommendationTargetType.MarketplaceListing,
                integrationEvent.ListingId,
                cancellationToken);
        }
    }
}
