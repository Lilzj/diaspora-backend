using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Moderation;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Modules.Community.Extensibility.Moderation;

/// <summary>
/// Runs the registered review providers.
/// <para>
/// Providers run in order and the <b>strictest answer wins</b> — a rejection beats a flag beats
/// a clean. Not first-wins and not last-wins: with several classifiers registered, one of them
/// objecting is the whole reason to run several.
/// </para>
/// <para>
/// With no provider registered the answer is <see cref="ContentReviewStatus.Unavailable"/>,
/// never clean.
/// </para>
/// <para>
/// <b>It decides nothing (7 Nov 2026).</b> The policy seam that turned a review or a report count
/// into an automatic hide is gone, with the tenth-report hide it carried: content is hidden without
/// a person only by Trust's moderation case, which records why and tells the owner.
/// </para>
/// </summary>
public sealed class ModerationPipeline
{
    private readonly List<IContentReviewProvider> _reviewers;
    private readonly IClock _clock;

    public ModerationPipeline(IEnumerable<IContentReviewProvider> reviewers, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(reviewers);

        _reviewers = reviewers
            .OrderBy(reviewer => reviewer.Order)
            .ThenBy(reviewer => reviewer.Name, StringComparer.Ordinal)
            .ToList();

        _clock = clock;
    }

    public bool HasReviewer => _reviewers.Count > 0;

    public async Task<ContentReview> ReviewAsync(
        ContentReviewRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_reviewers.Count == 0)
        {
            return ContentReview.Unavailable(request.Content, _clock.UtcNow);
        }

        ContentReview? strictest = null;

        foreach (var reviewer in _reviewers)
        {
            var result = await reviewer.ReviewAsync(request, cancellationToken);

            var review = ContentReview.Record(
                request.Content,
                result.Status,
                reviewer.Name,
                result.ProviderVersion,
                result.Confidence,
                result.Flags,
                _clock.UtcNow);

            if (strictest is null || Severity(review.Status) > Severity(strictest.Status))
            {
                strictest = review;
            }
        }

        return strictest!;
    }

    private static int Severity(ContentReviewStatus status) => status switch
    {
        ContentReviewStatus.Rejected => 4,
        ContentReviewStatus.Flagged => 3,
        ContentReviewStatus.Unavailable => 2,
        ContentReviewStatus.Clean => 1,
        _ => 0
    };
}
