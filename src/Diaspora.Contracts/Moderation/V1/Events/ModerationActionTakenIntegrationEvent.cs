using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Moderation.V1.Events;

/// <summary>
/// A moderation decision took effect on a piece of content (7 Nov 2026).
/// <para>
/// Published by Trust once per effective change: a hide, a confirmed removal, a restore, or a case
/// closed with no action. The owner is told from this, with the reason and how to appeal; reporters
/// are told the outcome without detail; the owning module resolves its report rows; paid placements
/// pause or resume.
/// </para>
/// </summary>
public sealed record ModerationActionTakenIntegrationEvent : IntegrationEvent
{
    public required Guid CaseId { get; init; }

    public required Guid DecisionId { get; init; }

    public required string ContentType { get; init; }

    public required Guid ContentId { get; init; }

    public string? ContentTitle { get; init; }

    public required string Area { get; init; }

    public required Guid OwnerUserId { get; init; }

    /// <summary>A <see cref="ModerationOutcome"/> name.</summary>
    public required string Outcome { get; init; }

    /// <summary>A <see cref="ModerationReasonCode"/> name.</summary>
    public required string ReasonCode { get; init; }

    /// <summary>The reason as the owner reads it.</summary>
    public required string ReasonLabel { get; init; }

    /// <summary>What the moderator wrote. Shown to the owner.</summary>
    public required string Note { get; init; }

    /// <summary>Null when the system acted: the automatic hide on many serious reports.</summary>
    public Guid? DecidedByUserId { get; init; }

    /// <summary>The second staff member who confirmed a removal.</summary>
    public Guid? ConfirmedByUserId { get; init; }

    /// <summary>Until when the owner may appeal. Null for a restore or no action.</summary>
    public DateTimeOffset? AppealDeadline { get; init; }

    /// <summary>Everyone who reported it, to be told the outcome without detail.</summary>
    public IReadOnlyList<Guid> ReporterUserIds { get; init; } = [];

    /// <summary>The module report rows the decision resolves.</summary>
    public IReadOnlyList<Guid> ModuleReportIds { get; init; } = [];

    public required DateTimeOffset TakenAt { get; init; }
}
