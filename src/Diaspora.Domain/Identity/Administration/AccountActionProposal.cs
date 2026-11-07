using Diaspora.Domain.Identity.Errors;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Domain.Identity.Administration;

/// <summary>An account action one member of staff proposed and a second decides.</summary>
public sealed record AccountActionProposalId(Guid Value) : StronglyTypedId(Value)
{
    public static AccountActionProposalId New() => new(Guid.NewGuid());

    public static AccountActionProposalId From(Guid value) => new(value);
}

/// <summary>What a proposal would do to the account.</summary>
public enum AccountActionKind
{
    Suspend = 0,
    Disable = 1,
    GrantRole = 2,
    Reactivate = 3
}

/// <summary>Where a proposal stands. Only <see cref="Pending"/> can move, and only once.</summary>
public enum AccountActionProposalStatus
{
    Pending = 0,
    Confirmed = 1,
    Declined = 2,
    Withdrawn = 3
}

/// <summary>
/// An action on a member's account that one member of staff proposes and a second confirms.
/// <para>
/// <b>The owner's rule (7 Nov 2026).</b> Suspending an account, closing one, reopening one, and
/// granting Admin or SuperAdmin each need a second member of staff. Until then one person could
/// take any of them alone, and nothing recorded that anybody else had looked. The proposal is the
/// record of both people: who proposed it and why, who confirmed or declined it and what they said.
/// </para>
/// <para>
/// <b>It holds the decision, not its effect.</b> Confirming changes the proposal; the account is
/// changed by the handler that confirms it, through the account's own methods, in the same unit of
/// work. Whether the decider may decide — a different person, holding the permission, above the
/// account in rank — is the application's to check, because it needs the decider's account.
/// </para>
/// </summary>
public sealed class AccountActionProposal : AggregateRoot<AccountActionProposalId>
{
    /// <summary>The proposer's note becomes the account's status reason, so it shares that limit.</summary>
    public const int MaxNoteLength = 512;

    public const int MaxDecisionNoteLength = 1000;

    public const int MaxReasonCodeLength = 64;

    public const int MaxRoleLength = 32;

    public AccountActionKind Kind { get; private set; }

    public UserId TargetUserId { get; private set; } = default!;

    public UserId ProposedBy { get; private set; } = default!;

    /// <summary>A moderation reason code's name. Null for a role grant, which is not a sanction.</summary>
    public string? ReasonCode { get; private set; }

    /// <summary>Why, in the proposer's words. Required: no staff action without a reason.</summary>
    public string Note { get; private set; } = string.Empty;

    /// <summary>When a proposed suspension ends. Null means until reviewed.</summary>
    public DateTimeOffset? Until { get; private set; }

    /// <summary>The role a <see cref="AccountActionKind.GrantRole"/> proposal gives.</summary>
    public string? Role { get; private set; }

    public AccountActionProposalStatus Status { get; private set; }

    public UserId? DecidedBy { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    public string? DecisionNote { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private AccountActionProposal() { }

    private AccountActionProposal(AccountActionProposalId id) : base(id) { }

    public static AccountActionProposal Propose(
        AccountActionKind kind,
        UserId targetUserId,
        UserId proposedBy,
        string? reasonCode,
        string note,
        DateTimeOffset? until,
        string? role,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(targetUserId);
        ArgumentNullException.ThrowIfNull(proposedBy);
        ArgumentException.ThrowIfNullOrWhiteSpace(note);

        if (kind == AccountActionKind.GrantRole)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(role);
        }

        return new AccountActionProposal(AccountActionProposalId.New())
        {
            Kind = kind,
            TargetUserId = targetUserId,
            ProposedBy = proposedBy,
            ReasonCode = string.IsNullOrWhiteSpace(reasonCode) ? null : reasonCode.Trim(),
            Note = Truncate(note.Trim(), MaxNoteLength),
            Until = kind == AccountActionKind.Suspend ? until : null,
            Role = kind == AccountActionKind.GrantRole ? role!.Trim() : null,
            Status = AccountActionProposalStatus.Pending,
            CreatedAt = now
        };
    }

    public bool IsPending => Status == AccountActionProposalStatus.Pending;

    /// <summary>A second member of staff agrees. The proposer never can.</summary>
    public Result Confirm(UserId confirmedBy, string? note, DateTimeOffset now) =>
        Decide(AccountActionProposalStatus.Confirmed, confirmedBy, note, now);

    /// <summary>A second member of staff disagrees, and nothing is done to the account.</summary>
    public Result Decline(UserId declinedBy, string? note, DateTimeOffset now) =>
        Decide(AccountActionProposalStatus.Declined, declinedBy, note, now);

    /// <summary>The proposer takes it back before anybody has decided.</summary>
    public Result Withdraw(UserId withdrawnBy, string? note, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(withdrawnBy);

        if (!IsPending)
        {
            return Result.Failure(IdentityAdministrationErrors.ProposalNotPending);
        }

        if (withdrawnBy != ProposedBy)
        {
            return Result.Failure(IdentityAdministrationErrors.OnlyProposerWithdraws);
        }

        Status = AccountActionProposalStatus.Withdrawn;
        DecidedBy = withdrawnBy;
        DecidedAt = now;
        DecisionNote = NoteOrNull(note);

        return Result.Success();
    }

    private Result Decide(
        AccountActionProposalStatus outcome, UserId decidedBy, string? note, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(decidedBy);

        if (!IsPending)
        {
            return Result.Failure(IdentityAdministrationErrors.ProposalNotPending);
        }

        if (decidedBy == ProposedBy)
        {
            return Result.Failure(IdentityAdministrationErrors.ProposerCannotDecide);
        }

        Status = outcome;
        DecidedBy = decidedBy;
        DecidedAt = now;
        DecisionNote = NoteOrNull(note);

        return Result.Success();
    }

    private static string? NoteOrNull(string? note) =>
        string.IsNullOrWhiteSpace(note) ? null : Truncate(note.Trim(), MaxDecisionNoteLength);

    private static string Truncate(string value, int length) =>
        value.Length <= length ? value : value[..length];
}
