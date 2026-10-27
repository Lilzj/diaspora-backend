using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.Events;

/// <summary>Raised when a moderator closes a report, upheld or not.</summary>
public sealed record ListingReportResolvedDomainEvent : DomainEvent
{
    public required ListingReportId ReportId { get; init; }

    public required MarketplaceListingId ListingId { get; init; }

    public required ListingReportStatus Status { get; init; }

    public required UserId ResolvedBy { get; init; }

    public string? Resolution { get; init; }

    public required DateTimeOffset ResolvedAt { get; init; }

    /// <summary>Who reported it. An actioned report pays them (REP-13).</summary>
    public required UserId ReporterId { get; init; }

    /// <summary>Whose listing it was, so a seller reporting their own listing is not paid.</summary>
    public required SellerProfileId SellerId { get; init; }
}
