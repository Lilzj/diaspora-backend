using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Moderation;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Modules.Community.Extensibility.Moderation;

/// <summary>
/// Runs the registered review providers and policies.
/// <para>
/// Providers run in order and the <b>strictest answer wins</b> — a rejection beats a flag beats
/// a clean. Not first-wins and not last-wins: with several classifiers registered, one of them
/// objecting is the whole reason to run several.
/// </para>
/// <para>
/// With no provider registered the answer is <see cref="ContentReviewStatus.Unavailable"/>,
/// never clean.
/// </para>
/// </summary>
public sealed class ModerationPipeline
{
    private readonly List<IContentReviewProvider> _reviewers;
    private readonly IReadOnlyList<IModerationPolicyProvider> _policies;
    private readonly IClock _clock;

    public ModerationPipeline(
        IEnumerable<IContentReviewProvider> reviewers,
        IEnumerable<IModerationPolicyProvider> policies,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(reviewers);
        ArgumentNullException.ThrowIfNull(policies);

        _reviewers = reviewers
            .OrderBy(reviewer => reviewer.Order)
            .ThenBy(reviewer => reviewer.Name, StringComparer.Ordinal)
            .ToList();

        _policies = policies
            .OrderBy(policy => policy.Order)
            .ThenBy(policy => policy.GetType().FullName, StringComparer.Ordinal)
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

    /// <summary>
    /// The action the policies agree on, or null. The strictest again — a policy that hides
    /// beats one that says nothing, because a policy exists to be enforced.
    /// </summary>
    public ModerationActionType? Decide(ModerationSignal signal)
    {
        ArgumentNullException.ThrowIfNull(signal);

        ModerationActionType? decision = null;

        foreach (var policy in _policies)
        {
            var action = policy.Evaluate(signal);

            if (action is null)
            {
                continue;
            }

            if (decision is null || ActionSeverity(action.Value) > ActionSeverity(decision.Value))
            {
                decision = action;
            }
        }

        return decision;
    }

    private static int Severity(ContentReviewStatus status) => status switch
    {
        ContentReviewStatus.Rejected => 4,
        ContentReviewStatus.Flagged => 3,
        ContentReviewStatus.Unavailable => 2,
        ContentReviewStatus.Clean => 1,
        _ => 0
    };

    private static int ActionSeverity(ModerationActionType action) => action switch
    {
        ModerationActionType.Reject => 5,
        ModerationActionType.Suspend => 4,
        ModerationActionType.Hide => 3,
        ModerationActionType.Archive => 2,
        _ => 1
    };
}
