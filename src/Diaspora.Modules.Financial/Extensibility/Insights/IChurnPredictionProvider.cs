using Diaspora.Modules.Financial.Domain.Identifiers;

namespace Diaspora.Modules.Financial.Extensibility.Insights;

/// <summary>
/// Predicts whether a subscription is about to lapse.
/// <para>
/// Declared with no implementation. The obvious use is a retention offer, which would be a promotion
/// this module already knows how to issue — so the seam needs nothing else to be useful the day
/// something implements it.
/// </para>
/// </summary>
public interface IChurnPredictionProvider
{
    /// <summary>0 to 1. Null when the provider has no opinion, which is not the same as zero.</summary>
    Task<decimal?> PredictChurnRiskAsync(
        SubscriptionId subscriptionId, CancellationToken cancellationToken = default);
}
