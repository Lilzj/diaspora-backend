using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Extensibility.Pricing;

namespace Diaspora.Modules.Financial.Extensibility.Settlement;

/// <summary>
/// Decides how an amount divides.
/// <para>
/// <b>The extension seam for revenue sharing, and the shipped implementation only ever produces one
/// shape: the platform's commission, and the rest to the counterparty.</b> Escrow, multi-party
/// splits, affiliate shares and referral cuts are all the same operation with a different strategy,
/// and none of them ships in this phase.
/// </para>
/// </summary>
public interface IRevenueDistributionStrategy
{
    string Name { get; }

    Task<RevenueSplit> DistributeAsync(
        Money gross,
        PricingContext context,
        Guid? counterpartyId,
        CancellationToken cancellationToken = default);
}
