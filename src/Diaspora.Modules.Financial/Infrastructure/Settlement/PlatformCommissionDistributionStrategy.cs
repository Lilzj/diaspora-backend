using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Extensibility.Pricing;
using Diaspora.Modules.Financial.Extensibility.Settlement;

namespace Diaspora.Modules.Financial.Infrastructure.Settlement;

/// <summary>
/// The only distribution the platform performs today: commission to the platform, the rest to the
/// counterparty.
/// <para>
/// <b>The counterparty's share is computed and recorded and nothing pays it out.</b> That is the
/// brief's instruction — design the seams, do not implement settlement — and it is also the honest
/// position: a payout needs a bank rail, a KYC record, a reversal path and a reconciliation job, none
/// of which exists. What this strategy buys is that the number is correct and stamped with the
/// policy version that produced it from the first sale, so a future payout engine has a history to
/// settle rather than a backfill to invent.
/// </para>
/// <para>
/// Multi-party splits, affiliate shares and escrow releases are all this interface with a different
/// implementation.
/// </para>
/// </summary>
public sealed class PlatformCommissionDistributionStrategy : IRevenueDistributionStrategy
{
    private readonly IFinancialPolicyEngine _policies;

    public PlatformCommissionDistributionStrategy(IFinancialPolicyEngine policies) =>
        _policies = policies;

    public string Name => "platform-commission";

    public async Task<RevenueSplit> DistributeAsync(
        Money gross,
        PricingContext context,
        Guid? counterpartyId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gross);
        ArgumentNullException.ThrowIfNull(context);

        var priced = await _policies.PriceAsync(context, cancellationToken);

        // An unresolved commission policy means the platform takes nothing rather than guessing a
        // rate — the counterparty keeps the lot, which is the failure direction that does not take
        // money from somebody by accident. The refusal is still visible: the split carries no policy
        // version, and the revenue rollup reports the gap.
        var platformShare = priced.IsSuccess
            ? priced.Value.Amount.LesserOf(gross)
            : Money.Zero(gross.Currency, gross.Exponent);

        return new RevenueSplit
        {
            Gross = gross,
            PlatformShare = platformShare,
            CounterpartyShare = gross.Subtract(platformShare),
            CounterpartyId = counterpartyId,
            PolicyVersion = priced.IsSuccess ? priced.Value.PolicyVersion : null
        };
    }
}
