namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>
/// One report about a job advert, as the moderation queue needs it.
/// <para>
/// <b>There is no reporter on this record.</b> Somebody who reported a fraudulent employer must
/// never become identifiable to them, and the safest way to keep that true is for the identity
/// never to reach a contract at all — it stays on the row, behind moderator authorisation.
/// </para>
/// </summary>
public sealed record JobReportResponse
{
    public required Guid ReportId { get; init; }

    public required Guid PostingId { get; init; }

    /// <summary>The advert's title, so a queue reads as work rather than as identifiers.</summary>
    public required string PostingTitle { get; init; }

    public required Guid EmployerId { get; init; }

    public required string EmployerName { get; init; }

    public required string Reason { get; init; }

    public string? Detail { get; init; }

    public required string Status { get; init; }

    /// <summary>
    /// Upheld reports against this employer, across every advert they have placed. The pattern
    /// is the signal in recruitment fraud, and it is invisible on a single card.
    /// </summary>
    public required int EmployerUpheldReports { get; init; }

    /// <summary>How many members have reported this advert.</summary>
    public required int TotalReportsOnPosting { get; init; }

    /// <summary>Where the advert stands: <c>Clear</c>, <c>Flagged</c> or <c>Hidden</c>.</summary>
    public required string PostingModerationState { get; init; }

    public string? Resolution { get; init; }

    public DateTimeOffset? ResolvedAt { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
