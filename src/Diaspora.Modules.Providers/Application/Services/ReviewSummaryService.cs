using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.Modules.Providers.Domain.Reviews;
using Diaspora.Modules.Providers.Domain.ValueObjects;
using Diaspora.Modules.Providers.Observability;

namespace Diaspora.Modules.Providers.Application.Services;

/// <summary>
/// Keeps a provider's rating rollup honest.
/// <para>
/// <b>Recomputes; never increments.</b> A summary maintained by <c>+1</c> on write drifts the
/// first time a review is edited from four stars to two, hidden by a moderator, withdrawn by its
/// author, or restored on appeal — and it drifts <em>silently</em>, because nothing ever compares
/// it to the truth. A rating nobody can reconcile is a rating nobody should trust, and on a
/// directory people choose a solicitor from that matters more than the query it costs.
/// </para>
/// <para>
/// The query is a single <c>GROUP BY rating</c> over an index that already exists for the
/// review page. Recomputing on every review change is cheap enough that the incremental
/// alternative buys nothing except the drift.
/// </para>
/// </summary>
public sealed class ReviewSummaryService
{
    private readonly IProviderReviewRepository _reviews;
    private readonly IProviderRecommendationRepository _recommendations;

    public ReviewSummaryService(
        IProviderReviewRepository reviews, IProviderRecommendationRepository recommendations)
    {
        _reviews = reviews;
        _recommendations = recommendations;
    }

    /// <summary>Recomputes and applies one provider's rollup, from the reviews table.</summary>
    public async Task RefreshAsync(Provider provider, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var distribution = await _reviews.GetRatingDistributionAsync(provider.Id, cancellationToken);

        provider.ApplyReviewSummary(ToSummary(distribution));

        ProviderTelemetry.ReviewSummariesRecomputed.Add(1);
    }

    /// <summary>Recomputes the recommendation count too. Used after a recommendation changes.</summary>
    public async Task RefreshRecommendationsAsync(
        Provider provider, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var counts = await _recommendations.CountForProvidersAsync(
            [provider.Id], cancellationToken);

        provider.ApplyRecommendationCount(
            counts.TryGetValue(provider.Id.Value, out var count) ? count : 0);
    }

    /// <summary>
    /// Recomputes many providers at once. The repair sweep's entry point.
    /// <para>
    /// Exists because the only defence against a rollup that has drifted for reasons nobody
    /// anticipated — a partial failure, a restored backup, a bug since fixed — is to rebuild it
    /// from the source of truth on a schedule.
    /// </para>
    /// </summary>
    public async Task RefreshManyAsync(
        IReadOnlyList<Provider> providers, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(providers);

        if (providers.Count == 0)
        {
            return;
        }

        var ids = providers.Select(provider => provider.Id).ToList();

        var distributions = await _reviews.GetRatingDistributionsAsync(ids, cancellationToken);
        var recommendationCounts = await _recommendations.CountForProvidersAsync(ids, cancellationToken);

        foreach (var provider in providers)
        {
            var distribution = distributions.TryGetValue(provider.Id.Value, out var found)
                ? found
                : new Dictionary<int, int>();

            provider.ApplyReviewSummary(ToSummary(distribution));

            provider.ApplyRecommendationCount(
                recommendationCounts.TryGetValue(provider.Id.Value, out var count) ? count : 0);
        }

        ProviderTelemetry.ReviewSummariesRecomputed.Add(providers.Count);
    }

    private static ProviderReviewSummary ToSummary(IReadOnlyDictionary<int, int> distribution) =>
        ProviderReviewSummary.FromDistribution(
            distribution.GetValueOrDefault(1),
            distribution.GetValueOrDefault(2),
            distribution.GetValueOrDefault(3),
            distribution.GetValueOrDefault(4),
            distribution.GetValueOrDefault(5));
}
