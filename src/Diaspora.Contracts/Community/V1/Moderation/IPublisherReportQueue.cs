using Diaspora.SharedKernel.Results;

namespace Diaspora.Contracts.Community.V1.Moderation;

/// <summary>
/// The reports about content published under one publisher's name, and the decisions on them.
/// <para>
/// Added 21 Oct 2026 for the per-community moderation queue. Community Programme owns the
/// <c>/communities</c> prefix and who moderates a community; Community owns reports, content and
/// the decision logic. This is the seam between them: Community implements it, Community Programme
/// calls it after it has checked that the caller may moderate the community.
/// </para>
/// <para>
/// <b>"Belongs to" is decided by the content, never by the caller.</b> A report is about a
/// publisher when the reported post was published as that publisher, or the reported comment sits
/// under such a post. Nothing a reporter or a moderator sends can move a report into a queue.
/// </para>
/// <para>
/// <b>No reporter identity crosses this seam.</b> Community moderators are members, not staff, and
/// the person who reported harassment must not be shown to somebody who can see who reported them.
/// </para>
/// </summary>
public interface IPublisherReportQueue
{
    /// <summary>
    /// One page of reports about the publisher's content, oldest first, grouped by the content and
    /// status they share. <paramref name="status"/> is the client's vocabulary
    /// (<c>Open</c>, <c>UnderReview</c>, <c>Actioned</c>, <c>Dismissed</c>, <c>Escalated</c>);
    /// null or blank means every status.
    /// </summary>
    Task<Result<PublisherReportPage>> ListAsync(
        Guid publisherId,
        string? status,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Decides a report about the publisher's content. <paramref name="decision"/> is the client's
    /// vocabulary (<c>dismiss</c>, <c>remove-content</c>, <c>warn</c>, <c>escalate</c>). A report
    /// about anything else answers not-found, exactly as a report that does not exist.
    /// </summary>
    Task<Result<PublisherReportSnapshot>> DecideAsync(
        Guid publisherId,
        Guid reportId,
        string? decision,
        string? note,
        Guid moderatorId,
        CancellationToken cancellationToken = default);
}

/// <summary>One row of a publisher's queue: the content, and how many reports it carries.</summary>
/// <param name="ReportId">The oldest report in the row. The one a decision names.</param>
/// <param name="Status">Open, UnderReview, Actioned, Dismissed or Escalated.</param>
/// <param name="Reason">Spam, Harassment, Misinformation, OffTopic, Scam, HateSpeech or Other.</param>
/// <param name="ContentType">Community's wire name for the content type, such as <c>post</c>.</param>
/// <param name="ReporterCount">How many reports share this content and status. Never who.</param>
/// <param name="Note">The deciding moderator's note, once decided.</param>
public sealed record PublisherReportSnapshot(
    Guid ReportId,
    string Status,
    string Reason,
    string ContentType,
    Guid ContentId,
    string? ContentExcerpt,
    int ReporterCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? Note);

public sealed record PublisherReportPage(
    IReadOnlyList<PublisherReportSnapshot> Items,
    string? NextCursor,
    bool HasMore);
