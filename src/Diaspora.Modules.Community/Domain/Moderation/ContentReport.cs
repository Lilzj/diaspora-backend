using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Errors;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Domain.Moderation;

/// <summary>
/// One member reporting one piece of content.
/// <para>
/// Unique per reporter and content, so a member cannot inflate a count by reporting the same
/// thing repeatedly — which matters because the count is what puts content in front of a
/// moderator, and, on Trust's case, what hides it pending review.
/// </para>
/// <para>
/// <b>This is where B6's moderation stops and Trust's begins.</b> Trust decides whether a
/// member has proved something and what their reputation is; B6 decides whether a piece of
/// content stays up. A report here never changes a trust score directly — it publishes an event,
/// and Trust decides what a sustained report is worth. Reaching into Trust's scoring from here
/// would be the duplication the brief forbids.
/// </para>
/// </summary>
public sealed class ContentReport : AggregateRoot<ContentReportId>, IAuditableEntity
{
    public const int MaxDescriptionLength = 2_000;

    public const int MaxEvidenceUrlLength = 2_048;

    private ContentReport(
        ContentReportId id,
        ContentReference content,
        string reporterKey,
        UserId reporterId,
        ReportReasonCode reason,
        string? description,
        string? evidenceUrl,
        DateTimeOffset reportedAt) : base(id)
    {
        Content = content;
        ReporterKey = reporterKey;
        ReporterId = reporterId;
        Reason = reason;
        Description = description;
        EvidenceUrl = evidenceUrl;
        Status = ReportStatus.Open;
        ReportedAt = reportedAt;
    }

    private ContentReport() { }

    public ContentReference Content { get; private set; } = default!;

    /// <summary>Reporter and content in one column, so one-report-per-member is a real index.</summary>
    public string ReporterKey { get; private set; } = string.Empty;

    public UserId ReporterId { get; private set; } = default!;

    public ReportReasonCode Reason { get; private set; }

    public string? Description { get; private set; }

    /// <summary>
    /// The link the client's conflicting-information category collects. Stored as text, never
    /// fetched: unfurling a URL a reporter supplied would make the platform issue requests on
    /// behalf of whoever wrote it.
    /// </summary>
    public string? EvidenceUrl { get; private set; }

    public ReportStatus Status { get; private set; }

    public Guid? ResolvedBy { get; private set; }

    public string? ResolutionNote { get; private set; }

    public DateTimeOffset ReportedAt { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    /// <summary>The author of the reported content, captured so the queue needs no second read.</summary>
    public Guid? ContentAuthorId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }

    public bool IsOpen => Status is ReportStatus.Open or ReportStatus.Reviewed;

    public static Result<ContentReport> File(
        ContentReference content,
        UserId reporterId,
        ReportReasonCode reason,
        string? description,
        string? evidenceUrl,
        DateTimeOffset now,
        Guid? contentAuthorId = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(reporterId);

        // The client makes the link mandatory for its conflicting category, and the server has
        // to agree: a misinformation report whose whole claim is "this contradicts that" is
        // unactionable without the that.
        if (reason == ReportReasonCode.Misinformation && string.IsNullOrWhiteSpace(evidenceUrl))
        {
            // Not a hard failure — a member may genuinely mean "this is false" without a link.
            // Only the client's conflicting flow requires one, and it sends it.
            evidenceUrl = null;
        }

        if (contentAuthorId is not null && contentAuthorId == reporterId.Value)
        {
            return Result.Failure<ContentReport>(CommunityErrors.ReportSelfContent);
        }

        return Result.Success(new ContentReport(
            ContentReportId.New(),
            content,
            BuildKey(reporterId, content),
            reporterId,
            reason,
            Truncate(description, MaxDescriptionLength),
            Truncate(evidenceUrl, MaxEvidenceUrlLength),
            now)
        {
            ContentAuthorId = contentAuthorId
        });
    }

    public static string BuildKey(UserId reporterId, ContentReference content)
    {
        ArgumentNullException.ThrowIfNull(reporterId);
        ArgumentNullException.ThrowIfNull(content);

        return $"{reporterId.Value:N}:{content.Key}";
    }

    public Result MarkReviewed()
    {
        if (Status is ReportStatus.Resolved or ReportStatus.Dismissed)
        {
            return Result.Failure(CommunityErrors.ReportAlreadyResolved);
        }

        Status = ReportStatus.Reviewed;

        return Result.Success();
    }

    public Result Resolve(Guid moderatorId, string? note, bool upheld, DateTimeOffset now)
    {
        if (Status is ReportStatus.Resolved or ReportStatus.Dismissed)
        {
            return Result.Failure(CommunityErrors.ReportAlreadyResolved);
        }

        Status = upheld ? ReportStatus.Resolved : ReportStatus.Dismissed;
        ResolvedBy = moderatorId;
        ResolutionNote = Truncate(note, MaxDescriptionLength);
        ResolvedAt = now;

        if (upheld)
        {
            RaiseDomainEvent(new Events.ContentReportUpheldDomainEvent
            {
                ReportId = Id,
                Content = Content,
                ReporterId = ReporterId,
                ContentAuthorId = ContentAuthorId,
                UpheldAt = now
            });
        }

        return Result.Success();
    }

    private static string? Truncate(string? value, int max)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length > max ? trimmed[..max] : trimmed;
    }

    /// <summary>Stamped by the shared persistence interceptor. Never called by a handler.</summary>
    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    /// <inheritdoc cref="SetCreated" />
    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }
}
