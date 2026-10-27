using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Rewards;

/// <summary>
/// A staff points grant above the <c>reward.grant</c> threshold, waiting for a second Admin (RWD-10).
/// <para>
/// <b>Asking grants nothing.</b> The points land when a different Admin approves, through the same
/// <see cref="RewardAccount.Grant"/> as any staff grant, naming both hands. That is why the request
/// carries everything the grant needs, and why the limits are checked again at approval: the day a
/// grant counts against is the day it lands.
/// </para>
/// <para>
/// <b>Its grant reference is unique across requests,</b> so a retried request replays rather than
/// asking twice, and the ledger's own unique reference stays the last word on paying once.
/// </para>
/// </summary>
public sealed class RewardGrantRequest : AggregateRoot<RewardGrantRequestId>, IAuditableEntity
{
    private RewardGrantRequest(
        RewardGrantRequestId id,
        Guid memberUserId,
        long points,
        string source,
        string grantReference,
        string reason,
        Guid requestedByUserId,
        DateTimeOffset requestedAt) : base(id)
    {
        MemberUserId = memberUserId;
        Points = points;
        Source = source;
        GrantReference = grantReference;
        Reason = reason;
        RequestedByUserId = requestedByUserId;
        RequestedAt = requestedAt;
        CreatedAt = requestedAt;
        Status = RewardGrantRequestStatus.Pending;
    }

    private RewardGrantRequest() { }

    /// <summary>Who the points are for.</summary>
    public Guid MemberUserId { get; private set; }

    public long Points { get; private set; }

    public string Source { get; private set; } = string.Empty;

    /// <summary>The grant's reference, as it will be on the entry. Unique across requests.</summary>
    public string GrantReference { get; private set; } = string.Empty;

    /// <summary>Why the member is being given these points.</summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>The Admin who asked. The grant is theirs, and counts toward their day.</summary>
    public Guid RequestedByUserId { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    public RewardGrantRequestStatus Status { get; private set; }

    /// <summary>Who approved or rejected it. Null while pending, and on a withdrawal.</summary>
    public Guid? DecidedByUserId { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    /// <summary>Why it was declined or withdrawn, or anything the approver added.</summary>
    public string? DecisionNote { get; private set; }

    /// <summary>The <c>reward.grant</c> version that allowed the approved grant.</summary>
    public string? PolicyVersion { get; private set; }

    public bool IsPending => Status == RewardGrantRequestStatus.Pending;

    /// <summary>Bumped on every change. Two Admins deciding at once: one wins, one gets 409.</summary>
    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static RewardGrantRequest Raise(
        RewardGrantRequestId id,
        Guid memberUserId,
        long points,
        string source,
        string grantReference,
        string reason,
        Guid requestedByUserId,
        DateTimeOffset requestedAt)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(points);
        ArgumentException.ThrowIfNullOrWhiteSpace(grantReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return new RewardGrantRequest(
            id,
            memberUserId,
            points,
            string.IsNullOrWhiteSpace(source) ? "Unspecified" : source.Trim(),
            grantReference.Trim(),
            reason.Trim(),
            requestedByUserId,
            requestedAt);
    }

    /// <summary>A second Admin agreed. The caller grants the points in the same unit of work.</summary>
    public void Approve(Guid approvedByUserId, string? note, string? policyVersion, DateTimeOffset now)
    {
        RequirePending();

        Status = RewardGrantRequestStatus.Approved;
        PolicyVersion = string.IsNullOrWhiteSpace(policyVersion) ? null : policyVersion.Trim();
        Settle(approvedByUserId, note, now);
    }

    public void Reject(Guid decidedByUserId, string note, DateTimeOffset now)
    {
        RequirePending();

        Status = RewardGrantRequestStatus.Rejected;
        Settle(decidedByUserId, note, now);
    }

    /// <summary>The Admin who asked took it back. Nobody else decided, so nobody is named.</summary>
    public void Withdraw(string note, DateTimeOffset now)
    {
        RequirePending();

        Status = RewardGrantRequestStatus.Withdrawn;
        Settle(decidedByUserId: null, note, now);
    }

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

    private void RequirePending()
    {
        if (!IsPending)
        {
            throw new DomainException(
                $"A grant request that is {Status} has already been decided.",
                FinancialErrorCodes.RewardGrantRequestDecided);
        }
    }

    private void Settle(Guid? decidedByUserId, string? note, DateTimeOffset now)
    {
        DecidedByUserId = decidedByUserId;
        DecisionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        DecidedAt = now;
        Revision++;
        ModifiedAt = now;
    }
}
