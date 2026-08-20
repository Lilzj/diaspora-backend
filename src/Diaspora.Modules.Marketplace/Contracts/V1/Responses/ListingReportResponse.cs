namespace Diaspora.Modules.Marketplace.Contracts.V1.Responses;

/// <summary>
/// A report, as the moderation queue renders it.
/// <para>
/// <b>The reporter's identity is absent from every response but a staff one.</b> Who reported a
/// listing is the most retaliation-prone fact this module holds, and a seller must never be able to
/// learn it from any surface.
/// </para>
/// </summary>
public sealed record ListingReportResponse
{
    public required Guid ReportId { get; init; }

    public required Guid ListingId { get; init; }

    public required string ListingTitle { get; init; }

    public required Guid SellerId { get; init; }

    public required string SellerName { get; init; }

    /// <summary>
    /// <c>Fraud</c>, <c>Duplicate</c>, <c>Spam</c>, <c>Prohibited</c>, <c>Counterfeit</c>,
    /// <c>Miscategorised</c>, <c>Offensive</c> or <c>Other</c>.
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>The reporter's own words. The part a moderator actually reads.</summary>
    public string? Detail { get; init; }

    /// <summary><c>Open</c>, <c>Reviewing</c>, <c>Actioned</c> or <c>Dismissed</c>.</summary>
    public required string Status { get; init; }

    /// <summary>How many distinct members have reported this listing.</summary>
    public required int TotalReportsOnListing { get; init; }

    /// <summary>Who filed it. Staff view only, and absent everywhere else.</summary>
    public Guid? ReporterId { get; init; }

    public string? Resolution { get; init; }

    public DateTimeOffset? ResolvedAt { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
