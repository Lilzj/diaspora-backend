using Diaspora.Contracts.Moderation.V1;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Trust.Domain;

/// <summary>What a recorded decision was, in the life of a case.</summary>
public enum ModerationDecisionKind
{
    /// <summary>A moderator decided the case.</summary>
    Decision = 0,

    /// <summary>A moderator proposed removal; it waits for a second member of staff.</summary>
    Proposal = 1,

    /// <summary>A second member of staff confirmed the first one's decision.</summary>
    Confirmation = 2,

    /// <summary>A second member of staff did not, and put their own choice in its place.</summary>
    Overruled = 3,

    /// <summary>The system acted: the automatic hide after many serious reports.</summary>
    System = 4
}

/// <summary>
/// One decision on a case. Append-only: a case's history is every one of these, in order, with who
/// made it, what they chose, the reason code and the note the owner reads.
/// </summary>
public sealed class ModerationDecision : Entity<ModerationDecisionId>
{
    public const int MaxReasonLength = 1000;

    public ModerationItemId ItemId { get; private set; } = default!;

    /// <summary>Null when the system acted.</summary>
    public UserId? ModeratorId { get; private set; }

    public ModerationDecisionKind Kind { get; private set; }

    public ModerationOutcome? Outcome { get; private set; }

    public ModerationReasonCode? ReasonCode { get; private set; }

    /// <summary>The note: what the owner reads.</summary>
    public string? Reason { get; private set; }

    /// <summary>Kept for the decisions recorded before outcomes existed.</summary>
    public ModerationActionType Action { get; private set; }

    public ModerationQueueStatus PreviousStatus { get; private set; }

    public ModerationQueueStatus NewStatus { get; private set; }

    public DateTimeOffset DecidedAt { get; private set; }

    private ModerationDecision() { }

    internal static ModerationDecision Record(
        ModerationItemId itemId,
        UserId? moderatorId,
        ModerationDecisionKind kind,
        ModerationOutcome outcome,
        ModerationReasonCode reasonCode,
        string? note,
        ModerationQueueStatus previousStatus,
        ModerationQueueStatus newStatus,
        DateTimeOffset decidedAt)
    {
        ArgumentNullException.ThrowIfNull(itemId);

        var trimmed = string.IsNullOrWhiteSpace(note)
            ? null
            : note.Trim().Length <= MaxReasonLength ? note.Trim() : note.Trim()[..MaxReasonLength];

        return new ModerationDecision
        {
            Id = ModerationDecisionId.New(),
            ItemId = itemId,
            ModeratorId = moderatorId,
            Kind = kind,
            Outcome = outcome,
            ReasonCode = reasonCode,
            Reason = trimmed,
            Action = LegacyActionFor(outcome),
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            DecidedAt = decidedAt
        };
    }

    private static ModerationActionType LegacyActionFor(ModerationOutcome outcome) => outcome switch
    {
        ModerationOutcome.NoAction => ModerationActionType.Approve,
        ModerationOutcome.HidePendingReview => ModerationActionType.Hide,
        ModerationOutcome.Remove => ModerationActionType.Reject,
        ModerationOutcome.Restore => ModerationActionType.Restore,
        _ => ModerationActionType.Escalate
    };
}
