using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Trust.Domain;

/// <summary>
/// One moderator's recorded action on a queue item.
/// <para>
/// Append-only. The decision history is what an appeal is argued against, so a reversed
/// decision must leave both rows behind — the original and the reversal.
/// </para>
/// </summary>
public sealed class ModerationDecision : Entity<ModerationDecisionId>
{
    public const int MaxReasonLength = 1000;

    public ModerationItemId ItemId { get; private set; } = default!;

    public UserId ModeratorId { get; private set; } = default!;

    public ModerationActionType Action { get; private set; }

    public string? Reason { get; private set; }

    public ModerationQueueStatus PreviousStatus { get; private set; }

    public ModerationQueueStatus NewStatus { get; private set; }

    public DateTimeOffset DecidedAt { get; private set; }

    private ModerationDecision() { }

    private ModerationDecision(
        ModerationDecisionId id,
        ModerationItemId itemId,
        UserId moderatorId,
        ModerationActionType action,
        string? reason,
        ModerationQueueStatus previousStatus,
        ModerationQueueStatus newStatus,
        DateTimeOffset decidedAt) : base(id)
    {
        ItemId = itemId;
        ModeratorId = moderatorId;
        Action = action;
        Reason = reason;
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        DecidedAt = decidedAt;
    }

    internal static ModerationDecision Record(
        ModerationItemId itemId,
        UserId moderatorId,
        ModerationActionType action,
        string? reason,
        ModerationQueueStatus previousStatus,
        ModerationQueueStatus newStatus,
        DateTimeOffset decidedAt)
    {
        ArgumentNullException.ThrowIfNull(itemId);
        ArgumentNullException.ThrowIfNull(moderatorId);

        var trimmed = string.IsNullOrWhiteSpace(reason)
            ? null
            : reason.Trim().Length <= MaxReasonLength ? reason.Trim() : reason.Trim()[..MaxReasonLength];

        return new ModerationDecision(
            ModerationDecisionId.New(), itemId, moderatorId, action, trimmed,
            previousStatus, newStatus, decidedAt);
    }
}
