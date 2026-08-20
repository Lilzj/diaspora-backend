namespace Diaspora.Modules.Housing.Contracts.V1.Responses;

/// <summary>
/// A report, as a moderator sees it.
/// <para>
/// There is no member-facing read path for reports at all - not even for a landlord to see reports
/// against their own listing, because a landlord who could enumerate them could work out who filed
/// them.
/// </para>
/// </summary>
public sealed record HousingReportResponse
{
    public required Guid Id { get; init; }

    public required Guid ListingId { get; init; }

    public required string ListingTitle { get; init; }

    public required Guid LandlordId { get; init; }

    public required string LandlordName { get; init; }

    /// <summary>Scam first. The one report type where an hour in the queue matters.</summary>
    public required string Reason { get; init; }

    public string? Detail { get; init; }

    /// <summary>Open, UnderReview, Upheld or Dismissed.</summary>
    public required string Status { get; init; }

    /// <summary>Who filed it. Present only to a moderator, and never on any event.</summary>
    public Guid? ReportedBy { get; init; }

    public Guid? ResolvedBy { get; init; }

    public DateTimeOffset? ResolvedAt { get; init; }

    public string? Resolution { get; init; }

    /// <summary>How many upheld reports this landlord already has. The queue most useful column.</summary>
    public required int LandlordUpheldReports { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
