using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Moderation.V1.Events;

/// <summary>
/// A removal stood: the appeal window closed with no appeal, or the appeal was not upheld
/// (7 Nov 2026). Only now do consequences follow. Financial forfeits the item's paused paid
/// placements; the owner is told what it cost them.
/// </summary>
public sealed record ModerationDecisionStoodIntegrationEvent : IntegrationEvent
{
    public required Guid CaseId { get; init; }

    public required string ContentType { get; init; }

    public required Guid ContentId { get; init; }

    public string? ContentTitle { get; init; }

    public required string Area { get; init; }

    public required Guid OwnerUserId { get; init; }

    public required string ReasonLabel { get; init; }

    /// <summary>A <c>StrikeLevel</c> name: Warning, First, Second, Third.</summary>
    public required string StrikeLevel { get; init; }

    /// <summary>Until when the owner may not post in <see cref="Area"/>, for a first or second strike.</summary>
    public DateTimeOffset? BlockedUntil { get; init; }

    /// <summary>The reputation points the removal cost (a positive number).</summary>
    public required int PointsLost { get; init; }

    /// <summary>Whether the appeal was refused (rather than the window closing with none).</summary>
    public required bool AfterAppeal { get; init; }

    public required DateTimeOffset StoodAt { get; init; }
}

/// <summary>
/// A member reached a third strike (7 Nov 2026). Identity turns this into an account suspension
/// proposal that two members of staff must confirm; nothing is suspended automatically.
/// </summary>
public sealed record MemberReachedThirdStrikeIntegrationEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }

    public required Guid CaseId { get; init; }

    public required string Area { get; init; }

    public required string ReasonCode { get; init; }

    public required DateTimeOffset At { get; init; }
}
