using Diaspora.SharedKernel.Results;

namespace Diaspora.Contracts.Moderation.V1;

/// <summary>
/// One module's content, as Trust's moderation cases see it (7 Nov 2026).
/// <para>
/// Trust may not reference a vertical and no vertical may reference Trust, so this is the seam: each
/// module implements it for each kind of content it lets members publish, and Trust finds them all by
/// <see cref="ContentType"/>. Trust never learns what a listing is; it asks who owns this, and tells
/// the module what was decided.
/// </para>
/// </summary>
public interface IModerationTarget
{
    /// <summary>One of <see cref="ModerationContentTypes"/>.</summary>
    string ContentType { get; }

    /// <summary>One of <see cref="ModerationAreas"/>: where a strike for this content would apply.</summary>
    string Area { get; }

    /// <summary>The content's owner and current state, or null when it does not exist.</summary>
    Task<ModerationTargetSnapshot?> DescribeAsync(
        Guid contentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Carries out a decision with the module's own moderation state. Mutates and does not save: the
    /// caller's unit of work saves the decision and the change together.
    /// <para>
    /// <see cref="ModerationOutcome.HidePendingReview"/> uses the state the owner can fix and ask
    /// about; <see cref="ModerationOutcome.Remove"/> the one only staff lift;
    /// <see cref="ModerationOutcome.Restore"/> clears both; <see cref="ModerationOutcome.NoAction"/>
    /// clears an automatic flag. None deletes, archives, closes or cancels.
    /// </para>
    /// </summary>
    Task<Result<ModerationTargetChange>> ApplyAsync(
        Guid contentId,
        ModerationOutcome outcome,
        ModerationActor actor,
        CancellationToken cancellationToken = default);
}

/// <summary>What Trust needs to know about a piece of content.</summary>
public sealed record ModerationTargetSnapshot(
    Guid ContentId,
    Guid OwnerUserId,
    string? Title,
    string State);

/// <summary>
/// Who decided, on which case, and why. A null moderator is the system: the automatic hide on many
/// serious reports.
/// </summary>
public sealed record ModerationActor(
    Guid? ModeratorUserId,
    Guid CaseId,
    ModerationReasonCode ReasonCode,
    string Note);

/// <summary>The content's moderation state before and after, for the decision's record.</summary>
public sealed record ModerationTargetChange(string PreviousState, string NewState);
