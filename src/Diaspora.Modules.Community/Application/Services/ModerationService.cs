using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Community.Domain.Comments;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Errors;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Moderation;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Community.Extensibility.Content;
using Diaspora.Modules.Community.Extensibility.Moderation;
using Diaspora.Modules.Community.Integration;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Application.Services;

/// <summary>
/// Reporting, automatic policy, and moderator decisions.
/// <para>
/// The whole moderation workflow in one service, so the four verticals that will consume it get
/// the same duplicate rule, the same auto-hide threshold, the same append-only history and the
/// same appeal trail rather than four approximations of each.
/// </para>
/// <para>
/// <b>Trust is consumed, never duplicated.</b> A report never adjusts a reputation here; it
/// publishes an event and Trust decides what a pattern of upheld reports is worth. A decision
/// of <c>suspend</c> is recorded and published, and Identity acts on it.
/// </para>
/// </summary>
public sealed class ModerationService : ICommunityModerationService
{
    private readonly IReportRepository _reports;
    private readonly IModerationRepository _moderation;
    private readonly ICommunityPostRepository _posts;
    private readonly ICommentRepository _comments;
    private readonly ContentReferenceResolver _references;
    private readonly CommunityContentSourceRegistry _sources;
    private readonly ModerationPipeline _pipeline;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ModerationService(
        IReportRepository reports,
        IModerationRepository moderation,
        ICommunityPostRepository posts,
        ICommentRepository comments,
        ContentReferenceResolver references,
        CommunityContentSourceRegistry sources,
        ModerationPipeline pipeline,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _reports = reports;
        _moderation = moderation;
        _posts = posts;
        _comments = comments;
        _references = references;
        _sources = sources;
        _pipeline = pipeline;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<ContentReport>> ReportAsync(
        ContentReference content,
        UserId reporterId,
        ReportReasonCode reason,
        string? description = null,
        string? evidenceUrl = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(reporterId);

        var existing = await _reports.FindAsync(reporterId, content, cancellationToken);

        if (existing is not null)
        {
            return Result.Failure<ContentReport>(CommunityErrors.ReportDuplicate);
        }

        var descriptor = await _references.DescribeAsync(content, cancellationToken);

        if (descriptor is null || !descriptor.Exists)
        {
            return Result.Failure<ContentReport>(CommunityErrors.ContentNotFound);
        }

        var filed = ContentReport.File(
            content, reporterId, reason, description, evidenceUrl, _clock.UtcNow, descriptor.AuthorId);

        if (filed.IsFailure)
        {
            return filed;
        }

        await _reports.AddAsync(filed.Value, cancellationToken);

        var openCount = await _reports.CountOpenAsync(content, cancellationToken) + 1;

        await AdjustReportCountAsync(content, 1, cancellationToken);

        filed.Value.AddDomainEvent(new Domain.Events.ContentReportedDomainEvent
        {
            ReportId = filed.Value.Id,
            Content = content,
            ReporterId = reporterId,
            Reason = reason,
            OpenReportCount = openCount,
            ContentAuthorId = descriptor.AuthorId
        });

        // The automatic policy runs on the way in, not on a timer. A tenth report has to hide
        // content now; discovering it an hour later when a sweep runs is not moderation.
        var review = await _moderation.GetLatestReviewAsync(content, cancellationToken);

        var action = _pipeline.Decide(new ModerationSignal
        {
            Content = content,
            AuthorId = descriptor.AuthorId,
            OpenReportCount = openCount,
            DominantReason = reason,
            ReviewStatus = review?.Status ?? ContentReviewStatus.Pending,
            ReviewFlags = review?.ParsedFlags ?? [],
            ReviewConfidence = review?.Confidence ?? 0d,
            CountryCode = descriptor.CountryCode
        });

        if (action is not null)
        {
            await ApplyAsync(
                content,
                action.Value,
                ModerationSource.System,
                null,
                "Automatic: report threshold reached.",
                descriptor,
                cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return filed;
    }

    /// <summary>Applies a moderator's decision, records it, and tells the owning module.</summary>
    public async Task<Result<ModerationDecision>> DecideAsync(
        ContentReference content,
        ModerationActionType action,
        Guid moderatorId,
        string? reason,
        ContentReportId? reportId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var descriptor = await _references.DescribeAsync(content, cancellationToken);

        if (descriptor is null)
        {
            return Result.Failure<ModerationDecision>(CommunityErrors.ContentNotFound);
        }

        var decision = await ApplyAsync(
            content, action, ModerationSource.Staff, moderatorId, reason, descriptor, cancellationToken);

        if (decision.IsFailure)
        {
            return decision;
        }

        if (reportId is not null)
        {
            var report = await _reports.GetAsync(reportId, cancellationToken);

            var upheld = action is not (ModerationActionType.Approve or ModerationActionType.Restore);

            report?.Resolve(moderatorId, reason, upheld, _clock.UtcNow);

            if (report is not null)
            {
                await AdjustReportCountAsync(content, -1, cancellationToken);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return decision;
    }

    public Task<IReadOnlyList<ModerationDecision>> GetHistoryAsync(
        ContentReference content, CancellationToken cancellationToken = default) =>
        _moderation.ListDecisionsAsync(content, cancellationToken);

    public Task<int> CountOpenReportsAsync(
        ContentReference content, CancellationToken cancellationToken = default) =>
        _reports.CountOpenAsync(content, cancellationToken);

    private async Task<Result<ModerationDecision>> ApplyAsync(
        ContentReference content,
        ModerationActionType action,
        ModerationSource source,
        Guid? moderatorId,
        string? reason,
        ContentDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        var previous = "unknown";
        var next = "unknown";

        switch (content.Type)
        {
            case CommunityContentType.Post:
            {
                var post = await _posts.GetAsync(CommunityPostId.From(content.Id), cancellationToken);

                if (post is null)
                {
                    return Result.Failure<ModerationDecision>(CommunityErrors.PostNotFound);
                }

                previous = CommunityWireNames.ToWire(post.Status);
                var applied = post.ApplyModeration(action);

                if (applied.IsFailure)
                {
                    return Result.Failure<ModerationDecision>(applied.Error);
                }

                next = CommunityWireNames.ToWire(post.Status);
                break;
            }

            case CommunityContentType.Comment:
            {
                var comment = await _comments.GetAsync(CommentId.From(content.Id), cancellationToken);

                if (comment is null)
                {
                    return Result.Failure<ModerationDecision>(CommunityErrors.CommentNotFound);
                }

                previous = CommunityWireNames.ToWire(comment.Status);
                var applied = comment.ApplyModeration(action);

                if (applied.IsFailure)
                {
                    return Result.Failure<ModerationDecision>(applied.Error);
                }

                next = CommunityWireNames.ToWire(comment.Status);
                break;
            }

            default:
            {
                // Another module's content. Community records the decision and publishes it; the
                // owning module hides its own row if it chose to implement that.
                var owner = _sources.Find(content.Type);

                if (owner is not null)
                {
                    await owner.ApplyModerationAsync(content.Id, action, cancellationToken);
                }

                previous = CommunityWireNames.ToWire(descriptor.Visibility);
                next = CommunityWireNames.ToWire(action);
                break;
            }
        }

        var decision = ModerationDecision.Record(
            content,
            action,
            source,
            moderatorId,
            reason,
            previous,
            next,
            _clock.UtcNow,
            null,
            descriptor.AuthorId);

        await _moderation.AddDecisionAsync(decision, cancellationToken);

        return Result.Success(decision);
    }

    private async Task AdjustReportCountAsync(
        ContentReference content, int delta, CancellationToken cancellationToken)
    {
        switch (content.Type)
        {
            case CommunityContentType.Post:
            {
                var post = await _posts.GetAsync(CommunityPostId.From(content.Id), cancellationToken);
                post?.ApplyEngagement(counters => counters.WithReports(delta));
                return;
            }

            case CommunityContentType.Comment:
            {
                var comment = await _comments.GetAsync(CommentId.From(content.Id), cancellationToken);
                comment?.AdjustReports(delta);
                return;
            }

            default:
                return;
        }
    }
}
