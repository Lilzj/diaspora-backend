using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Housing.V1.Events;

/// <summary>
/// A property was reported.
/// <para>
/// <b>It carries no reporter identity, and that is not an omission.</b> This contract reaches
/// whatever subscribes to the bus, and a member who reported a rental scam must never become
/// identifiable to the person they reported. The reporter is on the report row, behind moderator
/// authorisation, and nowhere else in the platform.
/// </para>
/// </summary>
public sealed record HousingReportedIntegrationEvent : IntegrationEvent
{
    public required Guid ReportId { get; init; }

    public required Guid ListingId { get; init; }

    public required Guid LandlordId { get; init; }

    /// <summary>Scam, Duplicate, Misleading and the rest. Scam is acted on immediately.</summary>
    public required string Reason { get; init; }

    /// <summary>How many distinct members have now reported it.</summary>
    public required int TotalReports { get; init; }

    /// <summary>Whether this report moved the listing into the moderation queue.</summary>
    public required bool WasFlagged { get; init; }

    public required DateTimeOffset ReportedAt { get; init; }
}
