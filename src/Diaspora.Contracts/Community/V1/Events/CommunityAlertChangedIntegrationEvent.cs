using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Community.V1.Events;

/// <summary>
/// Something on the live alert map changed: an alert appeared, was amended, withdrawn, cancelled,
/// expired or taken down.
/// <para>
/// Published only for posts a public map shows — public, and with coordinates — which is the same
/// rule the nearby route applies. It carries where the alert is and how far it reaches, because the
/// consumer has to know which map cells to tell, and nothing a member wrote: the frame it becomes is a
/// signal to refetch, and a socket frame is the one place moderation and erasure cannot reach.
/// </para>
/// </summary>
public sealed record CommunityAlertChangedIntegrationEvent : IntegrationEvent
{
    public required Guid PostId { get; init; }

    /// <summary>
    /// <c>created</c>, <c>updated</c>, <c>cancelled</c>, <c>status.changed</c> or <c>removed</c> —
    /// the realtime seam's own change kinds, so the consumer passes it through untranslated.
    /// </summary>
    public required string ChangeKind { get; init; }

    /// <summary>
    /// A stable key saying why, never words a person typed: <c>amended</c>, <c>edited</c>,
    /// <c>withdrawn</c>, <c>expired</c>, <c>author</c> or <c>moderation</c>. Null when the kind says
    /// it all.
    /// </summary>
    public string? Reason { get; init; }

    public required double Latitude { get; init; }

    public required double Longitude { get; init; }

    /// <summary>How far the alert reaches, in metres. Null for an alert at its point.</summary>
    public int? AffectedRadiusMetres { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
