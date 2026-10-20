using Diaspora.Contracts.Community.V1.Moderation;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Community.Domain.Comments;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Errors;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Moderation;
using Diaspora.Modules.Community.Domain.Policies;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Application.Services;

/// <summary>
/// Community's answer to <see cref="IPublisherReportQueue"/>: the reports about one publisher's
/// content, and a moderator's decision on one of them (21 Oct 2026).
/// <para>
/// <b>The decision goes through <see cref="ModerationService.DecideAsync"/></b> — the same call the
/// platform's <c>POST /community/moderation/decisions</c> makes — so a community moderator's removal
/// is recorded in the same append-only history, publishes the same events and adjusts the same
/// counters as a staff one.
/// </para>
/// <para>
/// <b>Three things this adds around it, each because the shared call would do harm here.</b>
/// </para>
/// <list type="number">
/// <item><b>Every open report on the content is answered, not just the one named.</b> A decision is
/// about the content; leaving the other reports open would put the same post back in the queue for
/// a second, possibly different, decision.</item>
/// <item><b>Dismissing never brings content back that is not merely hidden.</b> The shared call's
/// <c>Approve</c> republishes an expired, archived or removed post. Dismissing a report about one
/// answers the report and leaves the content where it is.</item>
/// <item><b>Removing content that is already gone answers the reports without a second removal</b>,
/// which would publish a second deletion event for one post.</item>
/// </list>
/// </summary>
public sealed class PublisherReportQueue : IPublisherReportQueue
{
    private const int ExcerptLength = 140;

    private readonly IReportRepository _reports;
    private readonly ICommunityPostRepository _posts;
    private readonly ICommentRepository _comments;
    private readonly ModerationService _moderation;
    private readonly ContentReferenceResolver _references;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public PublisherReportQueue(
        IReportRepository reports,
        ICommunityPostRepository posts,
        ICommentRepository comments,
        ModerationService moderation,
        ContentReferenceResolver references,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _reports = reports;
        _posts = posts;
        _comments = comments;
        _moderation = moderation;
        _references = references;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<PublisherReportPage>> ListAsync(
        Guid publisherId,
        string? status,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseStatus(status, out var statuses, out var matchesNothing))
        {
            return Result.Failure<PublisherReportPage>(CommunityErrors.ReportStatusUnknown);
        }

        // UnderReview is a state the client declares and this platform never enters for a
        // community: a report is open, escalated or decided. Asking for it is a fair question with
        // an empty answer, not an error.
        if (matchesNothing || publisherId == Guid.Empty)
        {
            return Result.Success(new PublisherReportPage([], null, false));
        }

        var page = await _reports.ListAboutPublisherAsync(
            publisherId,
            statuses,
            FeedCursor.Decode(cursor),
            Math.Clamp(limit, 1, CommunityLimits.MaxFeedPageSize),
            cancellationToken);

        // One row per piece of content and status, in the order the oldest report arrived. Nine
        // reports about one post are one decision, so they are one row with a count of nine.
        var rows = page.Items
            .GroupBy(report => (report.Content.Key, report.Status))
            .ToList();

        var items = new List<PublisherReportSnapshot>(rows.Count);

        foreach (var row in rows)
        {
            var first = row.First();
            var all = await _reports.ListForContentAsync(first.Content, cancellationToken);

            items.Add(await SnapshotAsync(first, all, cancellationToken));
        }

        return Result.Success(new PublisherReportPage(items, page.NextCursor, page.HasMore));
    }

    public async Task<Result<PublisherReportSnapshot>> DecideAsync(
        Guid publisherId,
        Guid reportId,
        string? decision,
        string? note,
        Guid moderatorId,
        CancellationToken cancellationToken = default)
    {
        var parsed = ParseDecision(decision);

        if (parsed is null)
        {
            return Result.Failure<PublisherReportSnapshot>(CommunityErrors.ModerationDecisionUnknown);
        }

        var report = reportId == Guid.Empty
            ? null
            : await _reports.GetAsync(ContentReportId.From(reportId), cancellationToken);

        // Not-found for a report about somebody else's content, the same answer as for one that does
        // not exist: a moderator of one community learns nothing about another's queue by guessing.
        if (report is null
            || publisherId == Guid.Empty
            || !await _reports.IsAboutPublisherAsync(report.Content, publisherId, cancellationToken))
        {
            return Result.Failure<PublisherReportSnapshot>(CommunityErrors.ReportNotFound);
        }

        if (report.Status == ReportStatus.Reviewed)
        {
            return Result.Failure<PublisherReportSnapshot>(CommunityErrors.ReportEscalated);
        }

        if (!report.IsOpen)
        {
            return Result.Failure<PublisherReportSnapshot>(CommunityErrors.ReportAlreadyResolved);
        }

        if (parsed == Decision.Warn)
        {
            return Result.Failure<PublisherReportSnapshot>(CommunityErrors.ModerationWarnUnavailable);
        }

        var all = await _reports.ListForContentAsync(report.Content, cancellationToken);

        // Open siblings only. One already escalated is the platform's to decide now.
        var siblings = all
            .Where(other => other.Id != report.Id && other.Status == ReportStatus.Open)
            .ToList();

        var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        if (parsed == Decision.Escalate)
        {
            foreach (var escalated in siblings.Prepend(report))
            {
                escalated.MarkReviewed();
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(await SnapshotAsync(report, all, cancellationToken));
        }

        var upheld = parsed == Decision.RemoveContent;
        var state = await ReadStateAsync(report.Content, cancellationToken);

        var applies = upheld
            ? state is ContentState.Live or ContentState.Hidden or ContentState.Dormant
            : state is ContentState.Live or ContentState.Hidden;

        var answered = siblings.Count;

        if (applies)
        {
            var decided = await _moderation.DecideAsync(
                report.Content,
                upheld ? ModerationActionType.Reject : ModerationActionType.Approve,
                moderatorId,
                trimmedNote,
                report.Id,
                cancellationToken);

            if (decided.IsFailure)
            {
                return Result.Failure<PublisherReportSnapshot>(decided.Error);
            }
        }
        else
        {
            // Answered without touching the content. See the class note.
            var resolved = report.Resolve(moderatorId, trimmedNote, upheld, _clock.UtcNow);

            if (resolved.IsFailure)
            {
                return Result.Failure<PublisherReportSnapshot>(resolved.Error);
            }

            answered++;
        }

        foreach (var sibling in siblings)
        {
            sibling.Resolve(moderatorId, trimmedNote, upheld, _clock.UtcNow);
        }

        await AdjustReportCountAsync(report.Content, -answered, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(await SnapshotAsync(report, all, cancellationToken));
    }

    private async Task<PublisherReportSnapshot> SnapshotAsync(
        ContentReport report,
        IReadOnlyList<ContentReport> all,
        CancellationToken cancellationToken)
    {
        var same = all.Where(other => other.Status == report.Status).ToList();

        if (same.Count == 0)
        {
            same.Add(report);
        }

        var descriptor = await _references.DescribeAsync(report.Content, cancellationToken);

        var updated = same.Max(other => other.ResolvedAt ?? other.ModifiedAt);

        return new PublisherReportSnapshot(
            report.Id.Value,
            ToWireStatus(report.Status),
            ToWireReason(report.Reason),
            CommunityWireNames.ToWire(report.Content.Type),
            report.Content.Id,
            Excerpt(descriptor?.Title),
            same.Count,
            same.Min(other => other.ReportedAt),
            updated,
            report.ResolutionNote);
    }

    private async Task<ContentState> ReadStateAsync(
        ContentReference content, CancellationToken cancellationToken)
    {
        switch (content.Type)
        {
            case CommunityContentType.Post:
            {
                var post = await _posts.GetAsync(CommunityPostId.From(content.Id), cancellationToken);

                return post?.Status switch
                {
                    null => ContentState.Missing,
                    PostStatus.Published => ContentState.Live,
                    PostStatus.Hidden => ContentState.Hidden,
                    PostStatus.Removed or PostStatus.Deleted => ContentState.Gone,
                    _ => ContentState.Dormant
                };
            }

            case CommunityContentType.Comment:
            {
                var comment = await _comments.GetAsync(CommentId.From(content.Id), cancellationToken);

                return comment?.Status switch
                {
                    null => ContentState.Missing,
                    CommentStatus.Active or CommentStatus.Reported => ContentState.Live,
                    CommentStatus.Hidden => ContentState.Hidden,
                    _ => ContentState.Gone
                };
            }

            default:
                return ContentState.Missing;
        }
    }

    private async Task AdjustReportCountAsync(
        ContentReference content, int delta, CancellationToken cancellationToken)
    {
        if (delta == 0)
        {
            return;
        }

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

    /// <summary>The client's five states onto the four stored ones.</summary>
    private static bool TryParseStatus(
        string? status, out IReadOnlyCollection<ReportStatus> statuses, out bool matchesNothing)
    {
        matchesNothing = false;
        statuses = [];

        if (string.IsNullOrWhiteSpace(status))
        {
            return true;
        }

        switch (status.Trim().ToUpperInvariant())
        {
            case "OPEN":
                statuses = [ReportStatus.Open];
                return true;
            case "ESCALATED":
                statuses = [ReportStatus.Reviewed];
                return true;
            case "ACTIONED":
                statuses = [ReportStatus.Resolved];
                return true;
            case "DISMISSED":
                statuses = [ReportStatus.Dismissed];
                return true;
            case "UNDERREVIEW":
                matchesNothing = true;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// The stored states onto the client's. <see cref="ReportStatus.Reviewed"/> is written by
    /// nothing but an escalation (checked 21 Oct 2026), and it keeps the report in the platform
    /// queue, which lists Open and Reviewed — so it is exactly what Escalated means.
    /// </summary>
    private static string ToWireStatus(ReportStatus status) => status switch
    {
        ReportStatus.Open => "Open",
        ReportStatus.Reviewed => "Escalated",
        ReportStatus.Resolved => "Actioned",
        ReportStatus.Dismissed => "Dismissed",
        _ => "Open"
    };

    /// <summary>
    /// The eight stored reasons onto the client's seven. Outdated is the modal's "inaccurate", a
    /// kind of misinformation; the three it has no word for are Other rather than a guess.
    /// </summary>
    private static string ToWireReason(ReportReasonCode reason) => reason switch
    {
        ReportReasonCode.Spam => "Spam",
        ReportReasonCode.Harassment => "Harassment",
        ReportReasonCode.Misinformation or ReportReasonCode.Outdated => "Misinformation",
        _ => "Other"
    };

    private static Decision? ParseDecision(string? decision) =>
        decision?.Trim().ToLowerInvariant() switch
        {
            "dismiss" => Decision.Dismiss,
            "remove-content" => Decision.RemoveContent,
            "warn" => Decision.Warn,
            "escalate" => Decision.Escalate,
            _ => null
        };

    private static string? Excerpt(string? title)
    {
        var trimmed = title?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length > ExcerptLength ? trimmed[..ExcerptLength] + "…" : trimmed;
    }

    private enum Decision
    {
        Dismiss,
        RemoveContent,
        Warn,
        Escalate
    }

    /// <summary>Where the reported content stands, for deciding whether a decision touches it.</summary>
    private enum ContentState
    {
        /// <summary>Published, or a comment that is up.</summary>
        Live,

        /// <summary>Hidden, by the report threshold or a moderator. Dismissing brings it back.</summary>
        Hidden,

        /// <summary>A draft, expired or archived post. Dismissing must not republish it.</summary>
        Dormant,

        /// <summary>Removed or deleted already.</summary>
        Gone,

        /// <summary>No longer resolvable at all.</summary>
        Missing
    }
}
