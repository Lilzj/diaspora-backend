using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;
using Diaspora.Modules.CommunityProgramme.Domain.Events;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityProgramme.Domain.Communities;

/// <summary>Where an offer to hand over a community has got to.</summary>
public enum CommunityOwnershipTransferStatus
{
    /// <summary>Made, and waiting for the person it was made to.</summary>
    Offered = 1,

    /// <summary>They said yes. They own the community.</summary>
    Accepted = 2,

    /// <summary>They said no. The owner is told, and stays the owner.</summary>
    Declined = 3,

    /// <summary>The owner took it back before it was answered.</summary>
    Withdrawn = 4,

    /// <summary>Nobody answered in time. An unanswered offer must not sit open for ever.</summary>
    Expired = 5
}

/// <summary>
/// Handing a community to somebody who helps run it — 11 Sep 2026.
/// <para>
/// <b>The way out of a community you no longer want to own.</b> An owner cannot leave
/// (<c>CommunityMembership.Leave</c> refuses them) and cannot be deleted out of the job either: when
/// Diaspora refuses a deletion request, the owner is asked whether they would rather hand the
/// community over. This is that act.
/// </para>
/// <para>
/// <b>An offer, never an appointment.</b> It lands pending and the other person answers — the same
/// rule organisation invitations follow, and for the same reason: owning a community is work and a
/// responsibility, and nobody may be given it while they are asleep. Until they accept, the owner is
/// still the owner and nothing about the community has changed.
/// </para>
/// <para>
/// <b>It may only be offered to somebody who already helps run the place.</b> The caller checks that
/// — a moderator or an administrator of this community, whose membership is visible. Handing a
/// community to a stranger, or to somebody whose membership nobody can see, would be a way to move a
/// community quietly out of the hands of the people in it.
/// </para>
/// </summary>
public sealed class CommunityOwnershipTransfer
    : AggregateRoot<CommunityOwnershipTransferId>, IAuditableEntity
{
    public const int MessageMaximumLength = 500;

    private CommunityOwnershipTransfer(
        CommunityOwnershipTransferId id,
        CommunityId communityId,
        UserId offeredBy,
        UserId offeredTo,
        string? message,
        DateTimeOffset offeredAt,
        DateTimeOffset expiresAt)
        : base(id)
    {
        CommunityId = communityId;
        OfferedBy = offeredBy;
        OfferedTo = offeredTo;
        Message = message;
        OfferedAt = offeredAt;
        ExpiresAt = expiresAt;
        Status = CommunityOwnershipTransferStatus.Offered;
    }

    // EF Core.
    private CommunityOwnershipTransfer() { }

    public CommunityId CommunityId { get; private set; } = null!;

    /// <summary>The owner making the offer.</summary>
    public UserId OfferedBy { get; private set; } = null!;

    /// <summary>The moderator or administrator being asked.</summary>
    public UserId OfferedTo { get; private set; } = null!;

    /// <summary>A note from the owner, if they wrote one. Shown to the person asked, as written.</summary>
    public string? Message { get; private set; }

    public DateTimeOffset OfferedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public CommunityOwnershipTransferStatus Status { get; private set; }

    public DateTimeOffset? RespondedAt { get; private set; }

    /// <summary>Why they said no, if they said why. The owner reads it.</summary>
    public string? DeclineReason { get; private set; }

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

    public bool IsOpen => Status == CommunityOwnershipTransferStatus.Offered;

    public bool HasLapsed(DateTimeOffset now) => IsOpen && now >= ExpiresAt;

    /// <summary>
    /// Makes the offer. The caller has established that <paramref name="offeredBy"/> owns the
    /// community, that <paramref name="offeredTo"/> helps run it, and that no other offer is open.
    /// </summary>
    public static Result<CommunityOwnershipTransfer> Offer(
        CommunityOwnershipTransferId id,
        Community community,
        UserId offeredBy,
        UserId offeredTo,
        string? message,
        DateTimeOffset now,
        int expiryDays)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(community);
        ArgumentNullException.ThrowIfNull(offeredBy);
        ArgumentNullException.ThrowIfNull(offeredTo);

        if (community.Status == CommunityStatus.Archived)
        {
            return Result.Failure<CommunityOwnershipTransfer>(CommunityErrors.ArchivedIsFinal);
        }

        if (offeredBy == offeredTo)
        {
            return Result.Failure<CommunityOwnershipTransfer>(CommunityErrors.OwnershipOfferToSelf);
        }

        var transfer = new CommunityOwnershipTransfer(
            id, community.Id, offeredBy, offeredTo, Clip(message), now, now.AddDays(expiryDays));

        transfer.RaiseDomainEvent(new CommunityOwnershipOfferedDomainEvent
        {
            TransferId = id,
            CommunityId = community.Id,
            CommunityName = community.Name,
            CommunitySlug = community.Slug.Value,
            OfferedBy = offeredBy,
            OfferedTo = offeredTo,
            Message = transfer.Message,
            ExpiresAt = transfer.ExpiresAt
        });

        return Result.Success(transfer);
    }

    /// <summary>
    /// They accept. The roles move in the handler, which holds both memberships; this records the
    /// answer and announces it.
    /// </summary>
    public Result Accept(DateTimeOffset now, string communityName)
    {
        var open = RequireOpen(now);

        if (open.IsFailure)
        {
            return open;
        }

        Status = CommunityOwnershipTransferStatus.Accepted;
        RespondedAt = now;

        RaiseDecision(communityName, accepted: true);
        return Result.Success();
    }

    /// <summary>They say no. The owner stays the owner and is told.</summary>
    public Result Decline(DateTimeOffset now, string? reason, string communityName)
    {
        var open = RequireOpen(now);

        if (open.IsFailure)
        {
            return open;
        }

        Status = CommunityOwnershipTransferStatus.Declined;
        RespondedAt = now;
        DeclineReason = Clip(reason);

        RaiseDecision(communityName, accepted: false);
        return Result.Success();
    }

    /// <summary>The owner takes the offer back. Nobody is told: it was never acted on.</summary>
    public Result Withdraw(DateTimeOffset now)
    {
        if (!IsOpen)
        {
            return Result.Failure(CommunityErrors.OwnershipOfferAlreadyAnswered);
        }

        Status = CommunityOwnershipTransferStatus.Withdrawn;
        RespondedAt = now;
        return Result.Success();
    }

    /// <summary>Nobody answered in time. The sweep's act, not a person's.</summary>
    public Result Expire(DateTimeOffset now)
    {
        if (!HasLapsed(now))
        {
            return Result.Failure(CommunityErrors.OwnershipOfferAlreadyAnswered);
        }

        Status = CommunityOwnershipTransferStatus.Expired;
        RespondedAt = now;
        return Result.Success();
    }

    /// <summary>
    /// An offer that ran out of time is answered by nobody, and answering it late would hand over a
    /// community on the strength of a message from a month ago.
    /// </summary>
    private Result RequireOpen(DateTimeOffset now)
    {
        if (!IsOpen)
        {
            return Result.Failure(CommunityErrors.OwnershipOfferAlreadyAnswered);
        }

        return now >= ExpiresAt
            ? Result.Failure(CommunityErrors.OwnershipOfferExpired)
            : Result.Success();
    }

    private void RaiseDecision(string communityName, bool accepted) =>
        RaiseDomainEvent(new CommunityOwnershipDecidedDomainEvent
        {
            TransferId = Id,
            CommunityId = CommunityId,
            CommunityName = communityName,
            OfferedBy = OfferedBy,
            OfferedTo = OfferedTo,
            Accepted = accepted,
            DeclineReason = DeclineReason
        });

    private static string? Clip(string? value)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length <= MessageMaximumLength
            ? trimmed
            : trimmed[..MessageMaximumLength];
    }
}
