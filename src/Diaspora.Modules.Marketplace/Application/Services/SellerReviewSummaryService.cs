using Diaspora.Modules.Marketplace.Domain.Reviews;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.Modules.Marketplace.Domain.ValueObjects;

namespace Diaspora.Modules.Marketplace.Application.Services;

/// <summary>
/// Keeps a seller's rating rollup honest.
/// <para>
/// <b>Recomputes; never increments.</b> A summary maintained by <c>+1</c> on write drifts the
/// first time a review is edited from four stars to two, hidden by a moderator, or withdrawn by
/// its author — and it drifts <em>silently</em>, because nothing ever compares it to the truth.
/// A rating nobody can reconcile is a rating nobody should trust, and this one sits directly
/// above a payment button.
/// </para>
/// <para>
/// The query is a single <c>GROUP BY rating</c> over an index the review list already needs.
/// Recomputing on every review change is cheap enough that the incremental alternative buys
/// nothing except the drift.
/// </para>
/// </summary>
public sealed class SellerReviewSummaryService
{
    private readonly ISellerReviewRepository _reviews;

    public SellerReviewSummaryService(ISellerReviewRepository reviews) => _reviews = reviews;

    /// <summary>Recomputes and applies one seller's rollup, from the reviews table.</summary>
    public async Task RefreshAsync(
        SellerProfile seller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(seller);

        var distribution = await _reviews
            .GetRatingDistributionAsync(seller.Id, cancellationToken)
            .ConfigureAwait(false);

        seller.ApplyReviewSummary(ToSummary(distribution));
    }

    /// <summary>
    /// Recomputes many sellers at once. The repair sweep's entry point.
    /// <para>
    /// Exists because the only defence against a rollup that has drifted for reasons nobody
    /// anticipated — a partial failure, a restored backup, a bug since fixed — is to rebuild it
    /// from the source of truth on a schedule.
    /// </para>
    /// </summary>
    public async Task RefreshManyAsync(
        IReadOnlyList<SellerProfile> sellers, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sellers);

        foreach (var seller in sellers)
        {
            await RefreshAsync(seller, cancellationToken).ConfigureAwait(false);
        }
    }

    private static SellerReviewSummary ToSummary(IReadOnlyDictionary<int, int> distribution)
    {
        ArgumentNullException.ThrowIfNull(distribution);

        return SellerReviewSummary.FromDistribution(
            distribution.GetValueOrDefault(1),
            distribution.GetValueOrDefault(2),
            distribution.GetValueOrDefault(3),
            distribution.GetValueOrDefault(4),
            distribution.GetValueOrDefault(5));
    }
}
