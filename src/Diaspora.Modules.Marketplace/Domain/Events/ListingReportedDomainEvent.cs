using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.Events;

/// <summary>
/// Raised when a member reports a listing.
/// <para>
/// <b>The handler flags the listing; it does not hide it.</b> A single report is an allegation,
/// and a module where one report took a listing down would be one where any competitor could
/// clear the category. Hiding is a moderator's decision, made from the queue this event fills.
/// </para>
/// </summary>
public sealed record ListingReportedDomainEvent : DomainEvent
{
    public required ListingReportId ReportId { get; init; }

    public required MarketplaceListingId ListingId { get; init; }

    public required SellerProfileId SellerId { get; init; }

    public required UserId ReporterId { get; init; }

    public required ListingReportReason Reason { get; init; }

    /// <summary>How many distinct members have now reported this listing.</summary>
    public required int TotalReports { get; init; }

    public required DateTimeOffset ReportedAt { get; init; }
}
