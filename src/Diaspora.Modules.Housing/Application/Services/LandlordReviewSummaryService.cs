using Diaspora.Modules.Housing.Domain.Landlords;
using Diaspora.Modules.Housing.Domain.Reviews;
using Diaspora.Modules.Housing.Domain.ValueObjects;

namespace Diaspora.Modules.Housing.Application.Services;

/// <summary>
/// Keeps a landlord's rating rollup honest.
/// <para>
/// <b>Recomputes; never increments.</b> A summary maintained by <c>+1</c> on write drifts the
/// first time a review is edited from four stars to two, hidden by a moderator, or withdrawn by
/// its author — and it drifts <em>silently</em>, because nothing ever compares it to the truth.
/// A rating nobody can reconcile is a rating nobody should trust, and this one sits above a
/// deposit.
/// </para>
/// <para>
/// The query is a single <c>GROUP BY rating</c> over an index the review list already needs.
/// </para>
/// </summary>
public sealed class LandlordReviewSummaryService
{
    private readonly ILandlordReviewRepository _reviews;

    public LandlordReviewSummaryService(ILandlordReviewRepository reviews) => _reviews = reviews;

    /// <summary>Recomputes and applies one landlord's rollup, from the reviews table.</summary>
    public async Task RefreshAsync(
        LandlordProfile landlord, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(landlord);

        var distribution = await _reviews
            .GetRatingDistributionAsync(landlord.Id, cancellationToken)
            .ConfigureAwait(false);

        landlord.ApplyReviewSummary(ToSummary(distribution));
    }

    /// <summary>
    /// Recomputes many landlords at once. The repair sweep's entry point.
    /// <para>
    /// Exists because the only defence against a rollup that has drifted for reasons nobody
    /// anticipated is to rebuild it from the source of truth on a schedule.
    /// </para>
    /// </summary>
    public async Task RefreshManyAsync(
        IReadOnlyList<LandlordProfile> landlords, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(landlords);

        foreach (var landlord in landlords)
        {
            await RefreshAsync(landlord, cancellationToken).ConfigureAwait(false);
        }
    }

    private static LandlordReviewSummary ToSummary(IReadOnlyDictionary<int, int> distribution)
    {
        ArgumentNullException.ThrowIfNull(distribution);

        return LandlordReviewSummary.FromDistribution(
            distribution.GetValueOrDefault(1),
            distribution.GetValueOrDefault(2),
            distribution.GetValueOrDefault(3),
            distribution.GetValueOrDefault(4),
            distribution.GetValueOrDefault(5));
    }
}
