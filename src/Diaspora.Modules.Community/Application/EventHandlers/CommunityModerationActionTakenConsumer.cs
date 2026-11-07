using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Moderation.V1;
using Diaspora.Contracts.Moderation.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Community.Domain.Comments;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Moderation;
using Diaspora.Modules.Community.Domain.Policies;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Modules.Community.Application.EventHandlers;

/// <summary>
/// Closes the Community report rows a moderation decision answered (7 Nov 2026).
/// <para>
/// One consumer for every content type Community takes reports on: its own posts and comments,
/// guides, and the other modules' content reported through the modal. A decision's report ids may
/// include another module's rows for the same content (a job reported both here and in Jobs); only
/// the ids that are Community reports about this content are touched, and the other module resolves
/// its own.
/// </para>
/// <para>
/// A hide or a removal upholds them, which pays the reporters through Trust; no action dismisses
/// them. A restore leaves them as they are: it answers the owner, not the reports. A hide the system
/// made on its own resolves nothing, because no person has looked yet.
/// </para>
/// </summary>
public sealed class CommunityModerationActionTakenConsumer
    : IntegrationEventHandler<ModerationActionTakenIntegrationEvent>
{
    private readonly IReportRepository _reports;
    private readonly ICommunityPostRepository _posts;
    private readonly ICommentRepository _comments;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public CommunityModerationActionTakenConsumer(
        IReportRepository reports,
        ICommunityPostRepository posts,
        ICommentRepository comments,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _reports = reports;
        _posts = posts;
        _comments = comments;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    protected override async Task HandleAsync(
        ModerationActionTakenIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (!CommunityModerationReasons.TryParseContentType(integrationEvent.ContentType, out var type)
            || integrationEvent.DecidedByUserId is not { } decidedBy
            || integrationEvent.ModuleReportIds.Count == 0)
        {
            return;
        }

        bool upheld;

        switch (integrationEvent.Outcome)
        {
            case nameof(ModerationOutcome.HidePendingReview):
            case nameof(ModerationOutcome.Remove):
                upheld = true;
                break;
            case nameof(ModerationOutcome.NoAction):
                upheld = false;
                break;
            default:
                return;
        }

        var now = _clock.UtcNow;
        var resolution = upheld
            ? $"Upheld: {integrationEvent.ReasonLabel}."
            : "Looked at and no action taken.";

        var resolved = 0;

        foreach (var reportId in integrationEvent.ModuleReportIds.Distinct())
        {
            var report = await _reports.GetAsync(ContentReportId.From(reportId), cancellationToken);

            if (report is null
                || report.Content.Type != type
                || report.Content.Id != integrationEvent.ContentId)
            {
                continue;
            }

            // Already resolved rows refuse quietly; a second decision on the case resolves nothing twice.
            if (report.Resolve(decidedBy, resolution, upheld, now).IsSuccess)
            {
                resolved++;
            }
        }

        if (resolved == 0)
        {
            return;
        }

        await AdjustReportCountAsync(type, integrationEvent.ContentId, -resolved, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The post's or comment's open-report counter, as the community moderators' path keeps it.</summary>
    private async Task AdjustReportCountAsync(
        CommunityContentType type, Guid contentId, int delta, CancellationToken cancellationToken)
    {
        switch (type)
        {
            case CommunityContentType.Post:
            {
                var post = await _posts.GetAsync(CommunityPostId.From(contentId), cancellationToken);
                post?.ApplyEngagement(counters => counters.WithReports(delta));
                return;
            }

            case CommunityContentType.Comment:
            {
                var comment = await _comments.GetAsync(CommentId.From(contentId), cancellationToken);
                comment?.AdjustReports(delta);
                return;
            }

            default:
                return;
        }
    }
}
