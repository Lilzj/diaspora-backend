using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Conversations.Domain.Errors;
using Diaspora.Modules.Conversations.Domain.Events;
using Diaspora.Modules.Conversations.Domain.Identifiers;
using Diaspora.Modules.Conversations.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Conversations.Domain.Blocks;

/// <summary>
/// One member has decided to stop hearing from another.
/// <para>
/// <b>Directional, and asymmetry is the entire point.</b> A blocks B is not B blocks A: only A chose
/// it, only A can undo it, and B is never told. A symmetric model — one row meaning "these two do
/// not speak" — would let B lift a block A imposed, which is the failure mode that makes a block
/// worthless to the person who needed it.
/// </para>
/// <para>
/// <b>Its own aggregate rather than a flag on the conversation.</b> A block is about a person and
/// outlives every conversation with them: it must stop the <em>next</em> conversation from being
/// opened, which a per-conversation flag by definition cannot. It also has to be answerable without
/// loading anything — "may A message B" is asked before a conversation exists — which is why it is a
/// two-column indexed table and nothing more.
/// </para>
/// <para>
/// <b>Blocking is Conversations' decision, not Trust's.</b> Trust owns whether an account is
/// suspended, verified or banned platform-wide; this is one member's private preference about one
/// other member, and it carries no judgement the platform acts on elsewhere. Trust's account-level
/// verdicts are consulted separately, by <c>ConversationAccessService</c>.
/// </para>
/// </summary>
public sealed class ConversationBlock : AggregateRoot<ConversationBlockId>, IAuditableEntity
{
    private ConversationBlock(
        ConversationBlockId id,
        UserId blockerUserId,
        UserId blockedUserId,
        string? reason,
        DateTimeOffset blockedAt)
        : base(id)
    {
        BlockerUserId = blockerUserId;
        BlockedUserId = blockedUserId;
        Reason = reason;
        BlockedAt = blockedAt;
    }

    private ConversationBlock() { }

    /// <summary>The member who did the blocking. The only one who can undo it.</summary>
    public UserId BlockerUserId { get; private set; } = default!;

    public UserId BlockedUserId { get; private set; } = default!;

    /// <summary>
    /// Why, in the blocker's words.
    /// <para>
    /// Optional, private to the blocker, and never shown to the blocked member — the client renders
    /// it only on the blocker's own "Blocked Users" screen. A reason surfaced to the other party
    /// would turn blocking into a conversation, which is what somebody blocking is trying to end.
    /// </para>
    /// </summary>
    public string? Reason { get; private set; }

    public DateTimeOffset BlockedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static Result<ConversationBlock> Create(
        ConversationBlockId id,
        UserId blockerUserId,
        UserId blockedUserId,
        string? reason,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(blockerUserId);
        ArgumentNullException.ThrowIfNull(blockedUserId);

        if (blockerUserId == blockedUserId)
        {
            return Result.Failure<ConversationBlock>(ConversationErrors.SelfBlock);
        }

        var trimmed = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        if (trimmed is { Length: > ConversationLimits.MaxBlockReasonLength })
        {
            trimmed = trimmed[..ConversationLimits.MaxBlockReasonLength];
        }

        var block = new ConversationBlock(id, blockerUserId, blockedUserId, trimmed, now);

        block.RaiseDomainEvent(new ConversationBlockedDomainEvent
        {
            BlockId = block.Id,
            BlockerUserId = blockerUserId,
            BlockedUserId = blockedUserId,
            BlockedAt = now
        });

        return Result.Success(block);
    }

    /// <summary>
    /// Announces the removal.
    /// <para>
    /// The row is deleted by the repository rather than tombstoned. A lifted block leaves no trace
    /// on purpose: keeping a history of who somebody used to block is keeping a record of a private
    /// decision they have since reversed, and nothing in this platform reads it.
    /// </para>
    /// </summary>
    public void AnnounceRemoval(DateTimeOffset now) =>
        RaiseDomainEvent(new ConversationUnblockedDomainEvent
        {
            BlockerUserId = BlockerUserId,
            BlockedUserId = BlockedUserId,
            UnblockedAt = now
        });

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
}
