using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;
using Diaspora.Modules.CommunityProgramme.Domain.Events;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityProgramme.Domain.Communities;

/// <summary>Where a request to close a community has got to.</summary>
public enum CommunityDeletionStatus
{
    /// <summary>Made by the owner, waiting for a Diaspora administrator to decide.</summary>
    AwaitingReview = 1,

    /// <summary>
    /// Decided: it will be carried out. Not carried out yet — see
    /// <see cref="CommunityDeletionRequest.EffectiveAt"/>. The community goes on as normal until then.
    /// </summary>
    Approved = 2,

    /// <summary>Decided: the community stays. The owner is told why, in the administrator's words.</summary>
    Rejected = 3,

    /// <summary>The owner changed their mind before it was carried out.</summary>
    Withdrawn = 4,

    /// <summary>Carried out. The community is closed.</summary>
    Completed = 5
}

/// <summary>
/// An owner asking Diaspora to close their community — 11 Sep 2026, the product owner's decision.
/// <para>
/// <b>"A community might have grown so large that you cannot just delete them."</b> That sentence is
/// this whole type. Closing a community ends a place other people meet in, so the owner's request
/// starts a wait rather than performing anything: thirty days, during which <em>nothing changes</em>
/// — members join, post and are notified exactly as before — and a Diaspora administrator decides in
/// the meantime.
/// </para>
/// <para>
/// <b>Three separate facts, and none of them may be collapsed.</b> The owner asked (and why); an
/// administrator decided (and why); and the wait ran out. A request that is approved on day two is
/// still not carried out until day thirty, and a request nobody reviewed is never carried out at all
/// — the sweep only completes what was <see cref="CommunityDeletionStatus.Approved"/>. Deletion that
/// happened because nobody looked would be the opposite of a review.
/// </para>
/// <para>
/// <b>A refusal has to be readable.</b> <see cref="Reject"/> requires the administrator's reason,
/// because the owner is told it verbatim and "your request was declined" on its own leaves somebody
/// with a community they no longer want and nowhere to go. What they can do instead — hand the
/// community to somebody who helps run it — is <see cref="CommunityOwnershipTransfer"/>.
/// </para>
/// </summary>
public sealed class CommunityDeletionRequest
    : AggregateRoot<CommunityDeletionRequestId>, IAuditableEntity
{
    public const int ReasonMaximumLength = 1000;
    public const int ReasonMinimumLength = 10;

    private CommunityDeletionRequest(
        CommunityDeletionRequestId id,
        CommunityId communityId,
        UserId requestedBy,
        string reason,
        DateTimeOffset requestedAt,
        DateTimeOffset effectiveAt)
        : base(id)
    {
        CommunityId = communityId;
        RequestedBy = requestedBy;
        Reason = reason;
        RequestedAt = requestedAt;
        EffectiveAt = effectiveAt;
        Status = CommunityDeletionStatus.AwaitingReview;
    }

    // EF Core.
    private CommunityDeletionRequest() { }

    public CommunityId CommunityId { get; private set; } = null!;

    /// <summary>The owner who asked. Only an owner can.</summary>
    public UserId RequestedBy { get; private set; } = null!;

    /// <summary>Why, in the owner's words. Read by the administrator who decides.</summary>
    public string Reason { get; private set; } = string.Empty;

    public DateTimeOffset RequestedAt { get; private set; }

    /// <summary>
    /// The earliest moment this may be carried out: thirty days after it was asked for.
    /// <para>
    /// Fixed when the request is made rather than counted from the decision, so the wait a member was
    /// told about is the wait they get, whenever the review happens.
    /// </para>
    /// </summary>
    public DateTimeOffset EffectiveAt { get; private set; }

    public CommunityDeletionStatus Status { get; private set; }

    public UserId? ReviewedBy { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    /// <summary>The administrator's words. Shown to the owner verbatim — see the type summary.</summary>
    public string? DecisionReason { get; private set; }

    /// <summary>When it stopped being open: carried out, refused or withdrawn.</summary>
    public DateTimeOffset? ClosedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    /// <summary>Still going: asked and not yet answered, or answered and not yet carried out.</summary>
    public bool IsOpen =>
        Status is CommunityDeletionStatus.AwaitingReview or CommunityDeletionStatus.Approved;

    /// <summary>Approved, and the wait is over.</summary>
    public bool IsDue(DateTimeOffset now) =>
        Status == CommunityDeletionStatus.Approved && now >= EffectiveAt;

    /// <summary>
    /// Opens a request. The caller has already established that this is the community's owner and
    /// that no other request is open — neither is visible from here.
    /// </summary>
    public static Result<CommunityDeletionRequest> Open(
        CommunityDeletionRequestId id,
        Community community,
        UserId requestedBy,
        string? reason,
        DateTimeOffset now,
        int waitDays)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(community);
        ArgumentNullException.ThrowIfNull(requestedBy);

        if (community.Status == CommunityStatus.Archived)
        {
            return Result.Failure<CommunityDeletionRequest>(CommunityErrors.ArchivedIsFinal);
        }

        var trimmed = reason?.Trim();

        // Asked for, not required as a formality: the administrator reading this has no other way to
        // know whether a community is being closed because it was a mistake or because it has become
        // a problem, and those get different answers.
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length < ReasonMinimumLength)
        {
            return Result.Failure<CommunityDeletionRequest>(CommunityErrors.DeletionNeedsReason);
        }

        if (trimmed.Length > ReasonMaximumLength)
        {
            return Result.Failure<CommunityDeletionRequest>(CommunityErrors.DeletionReasonTooLong);
        }

        var request = new CommunityDeletionRequest(
            id, community.Id, requestedBy, trimmed, now, now.AddDays(waitDays));

        request.RaiseDomainEvent(new CommunityDeletionRequestedDomainEvent
        {
            RequestId = id,
            CommunityId = community.Id,
            CommunityName = community.Name,
            RequestedBy = requestedBy,
            Reason = trimmed,
            EffectiveAt = request.EffectiveAt
        });

        return Result.Success(request);
    }

    /// <summary>A Diaspora administrator agrees. The community closes when the wait is over.</summary>
    public Result Approve(UserId reviewedBy, string? reason, DateTimeOffset now, string communityName)
    {
        ArgumentNullException.ThrowIfNull(reviewedBy);

        if (Status != CommunityDeletionStatus.AwaitingReview)
        {
            return Result.Failure(CommunityErrors.DeletionAlreadyDecided);
        }

        Status = CommunityDeletionStatus.Approved;
        ReviewedBy = reviewedBy;
        ReviewedAt = now;
        DecisionReason = Clip(reason);

        RaiseDecision(communityName, approved: true);
        return Result.Success();
    }

    /// <summary>
    /// A Diaspora administrator refuses. The reason is required and reaches the owner as written.
    /// </summary>
    public Result Reject(UserId reviewedBy, string? reason, DateTimeOffset now, string communityName)
    {
        ArgumentNullException.ThrowIfNull(reviewedBy);

        if (Status != CommunityDeletionStatus.AwaitingReview)
        {
            return Result.Failure(CommunityErrors.DeletionAlreadyDecided);
        }

        var trimmed = reason?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return Result.Failure(CommunityErrors.DeletionDecisionNeedsReason);
        }

        Status = CommunityDeletionStatus.Rejected;
        ReviewedBy = reviewedBy;
        ReviewedAt = now;
        DecisionReason = Clip(trimmed);
        ClosedAt = now;

        RaiseDecision(communityName, approved: false);
        return Result.Success();
    }

    /// <summary>The owner changes their mind. Allowed at any point before it is carried out.</summary>
    public Result Withdraw(DateTimeOffset now)
    {
        if (!IsOpen)
        {
            return Result.Failure(CommunityErrors.DeletionAlreadyDecided);
        }

        Status = CommunityDeletionStatus.Withdrawn;
        ClosedAt = now;
        return Result.Success();
    }

    /// <summary>
    /// Carries it out. Refused before the wait is over, and refused for anything that was not
    /// approved — including a request nobody ever reviewed.
    /// </summary>
    public Result Complete(DateTimeOffset now)
    {
        if (!IsDue(now))
        {
            return Result.Failure(CommunityErrors.DeletionNotDue);
        }

        Status = CommunityDeletionStatus.Completed;
        ClosedAt = now;
        return Result.Success();
    }

    private void RaiseDecision(string communityName, bool approved) =>
        RaiseDomainEvent(new CommunityDeletionDecidedDomainEvent
        {
            RequestId = Id,
            CommunityId = CommunityId,
            CommunityName = communityName,
            OwnerUserId = RequestedBy,
            Approved = approved,
            DecisionReason = DecisionReason,
            EffectiveAt = EffectiveAt
        });

    private static string? Clip(string? value)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length <= ReasonMaximumLength
            ? trimmed
            : trimmed[..ReasonMaximumLength];
    }
}
