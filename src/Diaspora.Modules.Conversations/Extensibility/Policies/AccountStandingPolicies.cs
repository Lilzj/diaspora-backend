using Diaspora.Platform.Services.Abstractions;
using Diaspora.Platform.Services.Models;

namespace Diaspora.Modules.Conversations.Extensibility.Policies;

/// <summary>
/// Names for the two shipped policies, so a diagnostic and a test spell them the same way.
/// </summary>
public static class ConversationPolicyNames
{
    public const string InitiatorAccountStanding = "conversations.initiator-account-standing";

    public const string RecipientAccountStanding = "conversations.recipient-account-standing";
}

/// <summary>
/// The caller's own account has to be in order.
/// <para>
/// <b>The gap this closes was recorded as D-3 and read as a deployment's job.</b> B13.2 shipped
/// <see cref="IConversationPolicy"/> with nothing behind it and wrote down what was missing —
/// <em>"Trust's account-level bans do not currently prevent opening a conversation; per-conversation
/// blocking does."</em> So a member the platform had suspended for harassment could not post, could
/// not comment, could not list and could not review, and could still open a new thread with anybody
/// on the platform. Messaging was the one surface the suspension did not reach, because it was the
/// one surface built after the rule chain and never joined to it.
/// </para>
/// <para>
/// <b>Why this lives in Conversations and reads a kernel port.</b> The seam scans every platform
/// assembly, so Trust could have shipped the implementation — and then Trust would reference
/// Conversations, invert the module order, and own a decision about what a conversation is. Trust
/// states a standing; this module decides what that stops. <see cref="ITrustService"/> is the
/// published in-process contract for asking, and this assembly gains no project reference to
/// anything.
/// </para>
/// <para>
/// <b>What it refuses, and what it deliberately does not.</b> This is consulted on
/// <c>CreateConversation</c> and nowhere else, so it governs <em>opening a thread with somebody
/// new</em> — the act a restriction is imposed to stop — and never a reply inside a thread that
/// already exists. A restricted member can still answer their landlord. That asymmetry is the whole
/// design: the platform's own restriction rule is worded "may read and react, but not publish", and
/// initiating unsolicited contact is the publishing half.
/// </para>
/// <para>
/// <b><see cref="AccountStanding.UnderReview"/> is allowed.</b> Nothing has been taken from a member
/// waiting on a decision, and refusing them would make the platform's own queue into a punishment.
/// <see cref="AccountStanding.Unknown"/> is allowed too: it is what a member with no trust profile
/// yet returns, which includes everybody in the window between registering and being provisioned.
/// </para>
/// </summary>
public sealed class InitiatorAccountStandingPolicy : IConversationPolicy
{
    private readonly ITrustService _trust;

    public InitiatorAccountStandingPolicy(ITrustService trust) => _trust = trust;

    public string Name => ConversationPolicyNames.InitiatorAccountStanding;

    public async Task<ConversationPolicyDecision> EvaluateAsync(
        ConversationPolicyContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var standing = await _trust.GetAccountStandingAsync(context.InitiatorUserId, cancellationToken);

        // Named rather than compared. AccountStanding.UnderReview sorts above Restricted and is not
        // a restriction, so `>= Restricted` would refuse the one member who has done nothing wrong.
        return standing switch
        {
            AccountStanding.Suspended => ConversationPolicyDecision.Refuse(
                "Your account has been suspended for violating our Community Guidelines. You "
                + "cannot start new conversations."),

            AccountStanding.Restricted => ConversationPolicyDecision.Refuse(
                "Your account is under posting restrictions, so you cannot start new "
                + "conversations. You can still reply to your existing ones."),

            _ => ConversationPolicyDecision.Allow()
        };
    }
}

/// <summary>
/// Nobody starts a conversation with a suspended account.
/// <para>
/// <b>The half a suspension misses if only the caller is checked.</b> A suspended member cannot
/// reply — they cannot use the platform at all — so a thread opened towards one is a message into a
/// room with nobody in it, and the sender waits for an answer that cannot come. Worse on the
/// surfaces this module exists for: somebody enquiring about a room, a job or an item is being sent
/// to a counterpart the platform has already decided should not be transacting.
/// </para>
/// <para>
/// <b>The refusal says nothing about the recipient, and that is the load-bearing part.</b> Another
/// member's moderation state is theirs, and a message that read "this account is suspended" would
/// turn conversation-opening into a probe for who the platform has acted against. This refuses with
/// the wording a block already produces, and the handler maps every policy refusal onto the same
/// <c>Blocked</c> error code — so a suspended recipient, a member who blocked you, and a member you
/// blocked are indistinguishable from outside. That is the same reasoning
/// <c>ConversationAccessService</c> gives for answering 404 rather than 403 to a non-participant.
/// </para>
/// <para>
/// <b>Restricted is not refused here.</b> A restricted member may read and react; they can read a
/// message and answer it. Only a suspension stops the thread being of any use.
/// </para>
/// </summary>
public sealed class RecipientAccountStandingPolicy : IConversationPolicy
{
    private readonly ITrustService _trust;

    public RecipientAccountStandingPolicy(ITrustService trust) => _trust = trust;

    public string Name => ConversationPolicyNames.RecipientAccountStanding;

    public async Task<ConversationPolicyDecision> EvaluateAsync(
        ConversationPolicyContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var standing = await _trust.GetAccountStandingAsync(context.RecipientUserId, cancellationToken);

        return standing == AccountStanding.Suspended
            ? ConversationPolicyDecision.Refuse("You cannot start a conversation with this member.")
            : ConversationPolicyDecision.Allow();
    }
}
