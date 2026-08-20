using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.CommunityLife.V1.Events;

/// <summary>
/// An event finished, with its attendance settled.
/// <para>
/// The moment the figures stop moving, and therefore the moment every reputation and analytics
/// consumer wants them. Published from the sweep rather than from an organizer's action, because an
/// organizer who has just run a festival is not going to press a button.
/// </para>
/// </summary>
public sealed record EventCompletedIntegrationEvent : IntegrationEvent
{
    public required Guid CommunityEventId { get; init; }

    public Guid? OrganizationId { get; init; }

    public required Guid OrganizerUserId { get; init; }

    public required string CategoryKey { get; init; }

    public required string CountryCode { get; init; }

    public Guid? NeighborhoodAreaId { get; init; }

    /// <summary>Places committed. A total only — never who held them.</summary>
    public required int GoingCount { get; init; }

    public required int CheckedInCount { get; init; }

    public required int NoShowCount { get; init; }

    /// <summary>Commitments that turned up, or null when nobody committed.</summary>
    public double? AttendanceRate { get; init; }

    public required DateTimeOffset CompletedAt { get; init; }
}
