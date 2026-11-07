using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Community.Domain.Comments;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Errors;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Moderation;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Community.Extensibility.Content;
using Diaspora.Modules.Community.Integration;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Application.Services;

/// <summary>
/// Reporting, and community moderators' decisions.
/// <para>
/// One reporting workflow, so every module that takes reports through Community gets the same
/// duplicate rule and the same append-only history rather than approximations of each.
/// </para>
/// <para>
/// <b>Staff decide on Trust's moderation case, not here (7 Nov 2026).</b> Every report filed here
/// is also sent to Trust (<c>ContentReportFiledIntegrationEvent</c>), which keeps the one case per
/// piece of content, hides it pending review on many serious reports, and carries a staff decision
/// out through each module's <c>IModerationTarget</c>. The automatic hide on the tenth report and the
/// staff route that decided on any module's content, which both acted with no reason the owner
/// could read and no appeal, are gone. <see cref="DecideAsync"/> remains for the members who
/// moderate their own community, through <see cref="PublisherReportQueue"/>, and only for
/// Community's own posts and comments.
/// </para>
/// </summary>
public sealed class ModerationService : ICommunityModerationService
{
    private readonly IReportRepository _reports;
    private readonly IModerationRepository _moderation;
    private readonly ICommunityPostRepository _posts;
    private readonly ICommentRepository _comments;
    private readonly ContentReferenceResolver _references;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ModerationService(
        IReportRepository reports,
        IModerationRepository moderation,
        ICommunityPostRepository posts,
        ICommentRepository comments,
        ContentReferenceResolver references,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _reports = reports;
        _moderation = moderation;
        _posts = posts;
        _comments = comments;
        _references = references;
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
            ContentAuthorId = descriptor.AuthorId,
            ContentTitle = descriptor.Title
        });

        // Nothing is hidden here any more, however many reports arrive: the report reaches Trust's
        // case through ContentReportToTrustEventHandler, and the case hides it pending review on
        // ten serious reports, as the system, with the owner told why (7 Nov 2026).
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return filed;
    }

    /// <summary>
    /// Applies a community moderator's decision on one of Community's own posts or comments, and
    /// records it. Staff decide on the Trust case instead; nothing here acts on another module's
    /// content.
    /// </summary>
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
        string previous;
        string next;

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
                // Another module's content is moderated through its own Trust case and
                // IModerationTarget (7 Nov 2026). Recording a decision here that nothing carries
                // out would tell the owner something happened that did not.
                return Result.Failure<ModerationDecision>(CommunityErrors.ContentTypeUnsupported);
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
