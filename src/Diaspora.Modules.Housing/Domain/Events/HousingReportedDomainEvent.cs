using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.Events;

/// <summary>
/// Raised when a member reports a property.
/// <para>
/// <b>It deliberately carries no reporter identity.</b> The published contract that follows from
/// it reaches whatever subscribes to the bus, and a member who reported a rental scam must never
/// be identifiable to the person they reported. The reporter is on the report row, behind
/// moderator authorisation, and nowhere else.
/// </para>
/// </summary>
public sealed record HousingReportedDomainEvent : DomainEvent
{
    public required HousingReportId ReportId { get; init; }

    public required HousingListingId ListingId { get; init; }

    public required LandlordProfileId LandlordId { get; init; }

    public required HousingReportReason Reason { get; init; }

    /// <summary>How many distinct members have now reported it.</summary>
    public required int TotalReports { get; init; }

    public required DateTimeOffset ReportedAt { get; init; }
}
