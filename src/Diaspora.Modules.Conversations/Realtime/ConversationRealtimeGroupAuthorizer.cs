using Diaspora.Modules.Conversations.Domain.Conversations;
using Diaspora.Modules.Conversations.Domain.Identifiers;
using Diaspora.Platform.Communication;
using Diaspora.SharedKernel.Identity;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Conversations.Realtime;

/// <summary>
/// The answer B13's runtime was built to wait for, expressed as a claim on one channel family.
/// <para>
/// <b>This class is the whole of what unblocks conversation-scoped realtime.</b> The runtime cannot
/// answer "is this member in conversation 4f21…" — no module owned conversations when it shipped.
/// This one does, so it can. Typing, read receipts, delivery acknowledgements and message fan-out all
/// work because of the three questions below, with no change to the hub, the notifier or the client
/// contract.
/// </para>
/// <para>
/// <b>B13.3 changed how it plugs in, and the change is a correction.</b> It used to implement
/// <c>IRealtimeGroupAuthorizer</c> and replace B13's shipped default on last-registration-wins —
/// which worked for exactly one vertical. The third module to want a live channel would have
/// replaced <em>this</em>, and every conversation on the platform would have stopped relaying with
/// nothing logged. It now claims the <c>conversation:</c> prefix and answers only beneath it, so
/// Community Life's organization and event channels sit beside it rather than on top of it. The
/// module registration order no longer matters to correctness.
/// </para>
/// <para>
/// <b>Deny by default, in both senses.</b> A name this class does not recognise is refused, and every
/// failure path — a malformed identifier, a missing conversation, a thrown query — returns false
/// rather than propagating. The registry treats false as an ordinary answer; a throw would become an
/// error frame a client cannot tell apart from a server fault, and the safe direction for an
/// authorisation question is always "no".
/// </para>
/// </summary>
public sealed class ConversationRealtimeGroupAuthorizer : IRealtimeChannelAuthorizer
{
    /// <summary>
    /// How many participants one fan-out may name.
    /// <para>
    /// A direct message has two and a group thread has a policy ceiling well under this, so the cap
    /// is a backstop rather than a limit anybody reaches. It exists because the seam's contract says
    /// a subscriber list is bounded, and a contributor that answered "however many there are" would
    /// be the one that eventually held a transaction open across a very large thread.
    /// </para>
    /// </summary>
    private const int MaxSubscribers = 512;

    private readonly IConversationRepository _conversations;
    private readonly ILogger<ConversationRealtimeGroupAuthorizer> _logger;

    public ConversationRealtimeGroupAuthorizer(
        IConversationRepository conversations,
        ILogger<ConversationRealtimeGroupAuthorizer> logger)
    {
        _conversations = conversations;
        _logger = logger;
    }

    public string Module => ConversationsModuleMetadata.Name;

    public string ChannelPrefix => RealtimeChannels.ConversationPrefix;

    public async Task<bool> CanJoinAsync(
        UserId userId, string channel, CancellationToken cancellationToken = default)
    {
        if (userId is null)
        {
            return false;
        }

        // Parsed, never trusted. A client supplies the channel name, and a string that merely starts
        // with the right prefix is not an identifier.
        if (RealtimeChannels.IdentifierOf(channel, RealtimeChannels.ConversationPrefix)
            is not { } parsed)
        {
            return false;
        }

        try
        {
            var conversation = await _conversations.GetForParticipantAsync(
                ConversationId.From(parsed), userId, cancellationToken);

            return conversation is not null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // An authorisation question answers "no" on failure. See the summary.
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Could not decide whether a member may join a conversation channel. Refusing.");

            return false;
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// The conversation's participants.
    /// <para>
    /// Answered so that a notification addressed to a conversation resolves through the same path as
    /// one addressed to an organization. Nothing in this module uses it today — the message handlers
    /// already hold the recipient list from the domain event, which is cheaper and more precise — but
    /// a contributor that refused the question would make a conversation the one channel a future
    /// platform-wide announcement could not reach.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<UserId>> ListSubscribersAsync(
        string channel, int limit, CancellationToken cancellationToken = default)
    {
        if (limit <= 0
            || RealtimeChannels.IdentifierOf(channel, RealtimeChannels.ConversationPrefix)
                is not { } parsed)
        {
            return [];
        }

        try
        {
            var conversation = await _conversations.GetAsync(
                ConversationId.From(parsed), cancellationToken);

            if (conversation is null)
            {
                return [];
            }

            return
            [
                .. conversation.Participants
                    .Where(participant => participant.LeftAt is null)
                    .Select(participant => participant.UserId)
                    .Take(Math.Min(limit, MaxSubscribers))
            ];
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // A fan-out that cannot be resolved reaches nobody.
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception, "Could not list a conversation's participants. Nobody was addressed.");

            return [];
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Whether one member may be told about another's availability.
    /// <para>
    /// <b>Sharing a conversation is the relationship, and it is deliberately narrower than
    /// joining.</b> Channel membership discloses that a conversation exists; presence discloses when
    /// somebody is awake, which is a different and more durable kind of exposure. Requiring an active
    /// shared conversation means a member who blocked somebody stops leaking their habits to them the
    /// moment the block lands, because a blocked conversation is still shared — so the block check
    /// runs too.
    /// </para>
    /// <para>
    /// A member's own availability is answered by the Communication Platform's own contributor, and
    /// the registry takes the first yes — so this method does not repeat it.
    /// </para>
    /// </summary>
    public async Task<bool> CanObservePresenceAsync(
        UserId observer, UserId subject, CancellationToken cancellationToken = default)
    {
        if (observer is null || subject is null || observer == subject)
        {
            return false;
        }

        try
        {
            return await _conversations.ShareConversationAsync(observer, subject, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // Same reasoning as CanJoinAsync.
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception, "Could not decide whether a member may observe presence. Refusing.");

            return false;
        }
#pragma warning restore CA1031
    }
}
