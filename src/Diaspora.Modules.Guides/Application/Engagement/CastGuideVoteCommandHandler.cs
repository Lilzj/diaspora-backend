using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Analytics;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Votes;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.Engagement;

/// <summary>
/// Casts or changes a helpfulness vote and moves both counters in step.
/// <para>
/// <b>Authors may not vote on their own guides.</b> Not because the arithmetic would break, but
/// because a helpfulness score is a signal to other readers, and a signal its subject can
/// author is not a signal.
/// </para>
/// <para>
/// A vote that says what the member's existing vote already says is refused by the aggregate, so
/// the rollup is never recomputed for a no-op — an idempotent client retry costs nothing and
/// changes nothing.
/// </para>
/// </summary>
public sealed class CastGuideVoteCommandHandler
    : ICommandHandler<CastGuideVoteCommand, GuideEngagementResponse>
{
    private readonly IGuideRepository _guides;
    private readonly IGuideVoteRepository _votes;
    private readonly IGuideAnalyticsRepository _analytics;
    private readonly IUnitOfWork _unitOfWork;
    private readonly GuideAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public CastGuideVoteCommandHandler(
        IGuideRepository guides,
        IGuideVoteRepository votes,
        IGuideAnalyticsRepository analytics,
        IUnitOfWork unitOfWork,
        GuideAuthorizationService authorization,
        IGuidGenerator guids,
        IClock clock)
    {
        _guides = guides;
        _votes = votes;
        _analytics = analytics;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<GuideEngagementResponse>> Handle(
        CastGuideVoteCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<GuideEngagementResponse>(caller.Error);
        }

        var guideId = GuideId.From(command.GuideId);
        var guide = await _guides.GetAsync(guideId, cancellationToken);

        if (guide is null || !_authorization.CanSee(guide))
        {
            return Result.Failure<GuideEngagementResponse>(GuideErrors.NotFound);
        }

        if (guide.AuthorId == caller.Value)
        {
            return Result.Failure<GuideEngagementResponse>(GuideErrors.SelfVote);
        }

        if (GuideWireNames.ParseVoteKind(command.Request.Kind) is not { } kind)
        {
            return Result.Failure<GuideEngagementResponse>(GuideErrors.VoteUnchanged);
        }

        var existing = await _votes.GetByGuideAndVoterAsync(
            guideId, caller.Value, cancellationToken);

        var analytics = await _analytics.GetByGuideAsync(guideId, cancellationToken);

        if (existing is null)
        {
            var vote = GuideVote.Cast(
                GuideVoteId.From(_guids.NewSequentialGuid()),
                guideId,
                caller.Value,
                kind,
                _clock.UtcNow);

            if (vote.IsFailure)
            {
                return Result.Failure<GuideEngagementResponse>(vote.Error);
            }

            await _votes.AddAsync(vote.Value, cancellationToken);

            Apply(guide, analytics, previous: null, kind);
        }
        else
        {
            var previous = existing.Kind;
            var changed = existing.ChangeKind(kind);

            if (changed.IsFailure)
            {
                return Result.Failure<GuideEngagementResponse>(changed.Error);
            }

            Apply(guide, analytics, previous, kind);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new GuideEngagementResponse
        {
            Views = guide.Engagement.Views,
            UniqueViews = guide.Engagement.UniqueViews,
            HelpfulCount = guide.Engagement.HelpfulCount,
            NotHelpfulCount = guide.Engagement.NotHelpfulCount,
            BookmarkCount = guide.Engagement.BookmarkCount,
            ShareCount = guide.Engagement.ShareCount,
            CommentCount = guide.Engagement.CommentCount
        });
    }

    /// <summary>
    /// Turns "was X, is now Y" into the two deltas the rollups take. A change retracts the old
    /// answer as it records the new one, so the two counters always sum to the votes cast.
    /// </summary>
    private static void Apply(
        Guide guide, GuideAnalytics? analytics, GuideVoteKind? previous, GuideVoteKind current)
    {
        var helpful = Delta(GuideVoteKind.Helpful, previous, current);
        var notHelpful = Delta(GuideVoteKind.NotHelpful, previous, current);

        GuideEngagementService.ApplyVote(guide, analytics, helpful, notHelpful);
    }

    private static int Delta(GuideVoteKind kind, GuideVoteKind? previous, GuideVoteKind current)
    {
        var added = current == kind ? 1 : 0;
        var removed = previous == kind ? 1 : 0;

        return added - removed;
    }
}
