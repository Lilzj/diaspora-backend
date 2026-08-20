using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Community.V1.Events;

/// <summary>
/// Content was reported.
/// <para>
/// Carries the running open-report count, because that is what any consumer actually acts on —
/// one report is a queue item and ten is an auto-hide, and an event that only says a report
/// happened forces every subscriber to go and count.
/// </para>
/// <para>
/// Trust subscribes to this rather than B6 adjusting a reputation directly. Deciding what a
/// sustained pattern of upheld reports is worth is Trust's judgement, and making it here would
/// be the duplication the brief forbids.
/// </para>
/// </summary>
public sealed record CommunityContentReportedIntegrationEvent : IntegrationEvent
{
    public required Guid ReportId { get; init; }

    public required string ContentType { get; init; }

    public required Guid ContentId { get; init; }

    public required Guid ReporterId { get; init; }

    public required string Reason { get; init; }

    public required int OpenReportCount { get; init; }

    public Guid? ContentAuthorId { get; init; }
}
