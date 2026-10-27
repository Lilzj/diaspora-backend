using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.Events;

/// <summary>A moderator upheld a report against a home listing (REP-13).</summary>
public sealed record HousingReportUpheldDomainEvent : DomainEvent
{
    public required HousingReportId ReportId { get; init; }

    public required HousingListingId ListingId { get; init; }

    public required LandlordProfileId LandlordId { get; init; }

    public required UserId ReportedBy { get; init; }

    public required DateTimeOffset UpheldAt { get; init; }
}
