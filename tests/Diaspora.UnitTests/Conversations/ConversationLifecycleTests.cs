using Diaspora.Modules.Conversations;
using Diaspora.Modules.Conversations.Domain.Conversations;
using Diaspora.Modules.Conversations.Domain.Enums;
using Diaspora.Modules.Conversations.Domain.Errors;
using Diaspora.Modules.Conversations.Domain.Identifiers;
using Diaspora.Modules.Conversations.Domain.Policies;
using Diaspora.Modules.Conversations.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Conversations;

/// <summary>
/// The conversation aggregate's rules, without a database.
/// </summary>
public sealed class ConversationLifecycleTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 6, 10, 0, 0, TimeSpan.Zero);

    private static readonly UserId Buyer = UserId.From(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly UserId Seller = UserId.From(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    private static readonly UserId Stranger = UserId.From(Guid.Parse("33333333-3333-3333-3333-333333333333"));

    [Fact]
    public void Opening_a_conversation_with_yourself_is_refused()
    {
        var result = Open(Buyer, Buyer);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ConversationErrors.SelfConversation);
    }

    [Fact]
    public void Opening_creates_both_participants_with_the_roles_that_decide_a_request()
    {
        var conversation = Open().Value;

        conversation.Participants.Should().HaveCount(2);
        conversation.ParticipantFor(Buyer)!.Role.Should().Be(ParticipantRole.Initiator);
        conversation.ParticipantFor(Seller)!.Role.Should().Be(ParticipantRole.Recipient);
        conversation.Status.Should().Be(ConversationStatus.Active);
    }

    [Fact]
    public void A_conversation_that_requires_acceptance_starts_as_a_pending_request()
    {
        var conversation = Open(requiresAcceptance: true).Value;

        conversation.Status.Should().Be(ConversationStatus.PendingRequest);
        conversation.IsPendingRequest.Should().BeTrue();
    }

    /// <summary>
    /// The consent gate. Without this the request mechanism is a delay rather than a gate.
    /// </summary>
    [Fact]
    public void An_initiator_cannot_send_twice_into_an_unaccepted_request()
    {
        var conversation = Open(requiresAcceptance: true).Value;

        conversation.RecordMessage(ConversationMessageId.New(), Buyer, Now).IsSuccess
            .Should().BeTrue("the opening message is the request");

        var second = conversation.RecordMessage(ConversationMessageId.New(), Buyer, Now);

        second.IsFailure.Should().BeTrue();
        second.Error.Should().Be(ConversationErrors.RequestPending);
    }

    /// <summary>
    /// The recipient's composer <em>is</em> the accept button. Requiring a separate press would be a
    /// state the client cannot reach.
    /// </summary>
    [Fact]
    public void The_recipient_replying_accepts_the_request()
    {
        var conversation = Open(requiresAcceptance: true).Value;

        conversation.RecordMessage(ConversationMessageId.New(), Buyer, Now);
        conversation.RecordMessage(ConversationMessageId.New(), Seller, Now).IsSuccess
            .Should().BeTrue();

        conversation.Status.Should().Be(ConversationStatus.Active);
    }

    [Fact]
    public void Only_the_recipient_can_accept_a_request()
    {
        var conversation = Open(requiresAcceptance: true).Value;

        conversation.AcceptRequest(Buyer, Now).IsFailure.Should().BeTrue(
            "an initiator who could accept their own request would bypass the whole mechanism");

        conversation.AcceptRequest(Seller, Now).IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// The counter split. A message that advanced no counters leaves a thread the inbox never shows.
    /// </summary>
    [Fact]
    public void Recording_a_message_counts_it_unread_for_everybody_but_the_sender()
    {
        var conversation = Open().Value;
        var messageId = ConversationMessageId.New();

        var recipients = conversation.RecordMessage(messageId, Buyer, Now).Value;

        recipients.Should().ContainSingle().Which.Should().Be(Seller);
        conversation.ParticipantFor(Seller)!.UnreadCount.Should().Be(1);

        conversation.ParticipantFor(Buyer)!.UnreadCount.Should().Be(
            0, "a member's own message is not unread to them, on any of their devices");

        conversation.ParticipantFor(Buyer)!.LastReadMessageId.Should().Be(messageId);
        conversation.MessageCount.Should().Be(1);
        conversation.LastMessageId.Should().Be(messageId);
    }

    /// <summary>
    /// Archiving means filing, not muting. A new message must bring the thread back.
    /// </summary>
    [Fact]
    public void A_new_message_unarchives_for_the_recipient_but_does_not_unmute()
    {
        var conversation = Open().Value;

        conversation.Archive(Seller, Now);
        conversation.Mute(Seller, Now.AddHours(8), Now);

        conversation.RecordMessage(ConversationMessageId.New(), Buyer, Now);

        conversation.ParticipantFor(Seller)!.IsArchived.Should().BeFalse();
        conversation.ParticipantFor(Seller)!.IsMuted.Should().BeTrue();
    }

    [Fact]
    public void Reading_clears_the_counter_and_reports_what_it_cleared()
    {
        var conversation = Open().Value;

        conversation.RecordMessage(ConversationMessageId.New(), Buyer, Now);
        var last = ConversationMessageId.New();
        conversation.RecordMessage(last, Buyer, Now);

        var cleared = conversation.MarkRead(Seller, last, Now.AddMinutes(1));

        cleared.Value.Should().Be(2);
        conversation.ParticipantFor(Seller)!.UnreadCount.Should().Be(0);
        conversation.ParticipantFor(Seller)!.LastReadMessageId.Should().Be(last);
    }

    /// <summary>
    /// Reading implies receipt. A message cannot be read on a device that never received it.
    /// </summary>
    [Fact]
    public void Reading_also_advances_the_delivery_cursor()
    {
        var conversation = Open().Value;
        var messageId = ConversationMessageId.New();

        conversation.RecordMessage(messageId, Buyer, Now);
        conversation.MarkRead(Seller, messageId, Now.AddMinutes(1));

        conversation.ParticipantFor(Seller)!.LastDeliveredMessageId.Should().Be(messageId);
    }

    /// <summary>
    /// The badge must not go negative, and a read message must not be un-counted.
    /// </summary>
    [Fact]
    public void Withdrawing_a_message_only_un_counts_it_for_recipients_who_had_not_read_it()
    {
        var conversation = Open().Value;
        var first = ConversationMessageId.New();
        var second = ConversationMessageId.New();

        conversation.RecordMessage(first, Buyer, Now);
        conversation.RecordMessage(second, Buyer, Now);

        conversation.ParticipantFor(Seller)!.UnreadCount.Should().Be(2);

        conversation.WithdrawMessage(Buyer, second, Now);

        conversation.ParticipantFor(Seller)!.UnreadCount.Should().Be(1);

        conversation.MarkRead(Seller, second, Now);
        conversation.WithdrawMessage(Buyer, second, Now);

        conversation.ParticipantFor(Seller)!.UnreadCount.Should().Be(
            0, "a floor at zero is not a licence to decrement past what was counted");
    }

    [Fact]
    public void A_closed_conversation_refuses_new_messages_and_stays_readable()
    {
        var conversation = Open().Value;

        conversation.Close("entity_closed", Now);

        conversation.IsClosed.Should().BeTrue();
        conversation.RecordMessage(ConversationMessageId.New(), Buyer, Now).Error
            .Should().Be(ConversationErrors.Closed);

        conversation.HasActiveParticipant(Buyer).Should().BeTrue(
            "closing ends the conversation, it does not remove anybody from it");
    }

    [Fact]
    public void A_removed_participant_loses_the_conversation_and_their_pin()
    {
        var conversation = Open().Value;

        conversation.Pin(Seller, currentPinCount: 0, Now);
        conversation.RemoveParticipant(Seller, Now);

        conversation.HasActiveParticipant(Seller).Should().BeFalse();
        conversation.ParticipantFor(Seller)!.IsPinned.Should().BeFalse();

        conversation.Participants.Should().HaveCount(
            2, "the row survives so their old messages still have an author");
    }

    /// <summary>
    /// A member added back starts again. Restoring the old cursor would mark everything said in
    /// their absence as already seen.
    /// </summary>
    [Fact]
    public void Rejoining_does_not_restore_the_old_read_cursor()
    {
        var conversation = Open().Value;
        var messageId = ConversationMessageId.New();

        conversation.RecordMessage(messageId, Buyer, Now);
        conversation.MarkRead(Seller, messageId, Now);
        conversation.RemoveParticipant(Seller, Now);

        conversation.AddParticipant(Seller, ParticipantRole.Recipient, Now.AddDays(1));

        conversation.ParticipantFor(Seller)!.LastReadMessageId.Should().BeNull();
        conversation.ParticipantFor(Seller)!.UnreadCount.Should().Be(0);
    }

    [Fact]
    public void The_pin_limit_is_five_and_the_aggregate_enforces_it()
    {
        var conversation = Open().Value;

        conversation.Pin(Buyer, ConversationLimits.MaxPinnedConversations, Now).Error
            .Should().Be(ConversationErrors.PinLimitReached);

        conversation.Pin(Buyer, ConversationLimits.MaxPinnedConversations - 1, Now).IsSuccess
            .Should().BeTrue();

        conversation.ParticipantFor(Buyer)!.PinOrder.Should().Be(
            ConversationLimits.MaxPinnedConversations);
    }

    /// <summary>
    /// A timed mute expires without anything writing to the row. Trusting the boolean would silence
    /// somebody for a week on an eight-hour decision.
    /// </summary>
    [Fact]
    public void A_timed_mute_expires_against_the_clock_rather_than_against_a_flag()
    {
        var conversation = Open().Value;

        conversation.Mute(Seller, Now.AddHours(8), Now);

        var participant = conversation.ParticipantFor(Seller)!;

        participant.IsMutedAt(Now.AddHours(1)).Should().BeTrue();
        participant.IsMutedAt(Now.AddHours(9)).Should().BeFalse();
        participant.IsMuted.Should().BeTrue("the stored flag has not been rewritten");
    }

    [Fact]
    public void An_indefinite_mute_never_expires()
    {
        var conversation = Open().Value;

        conversation.Mute(Seller, until: null, Now);

        conversation.ParticipantFor(Seller)!.IsMutedAt(Now.AddYears(1)).Should().BeTrue();
    }

    [Fact]
    public void Somebody_who_is_not_a_participant_cannot_change_anything()
    {
        var conversation = Open().Value;

        conversation.Archive(Stranger, Now).Error.Should().Be(ConversationErrors.NotAParticipant);
        conversation.Mute(Stranger, null, Now).Error.Should().Be(ConversationErrors.NotAParticipant);
        conversation.MarkRead(Stranger, null, Now).Error.Should().Be(ConversationErrors.NotAParticipant);
    }

    // Fully qualified from the global root: inside Diaspora.UnitTests.Conversations, `SharedKernel`
    // binds to Diaspora.UnitTests.SharedKernel before it ever reaches Diaspora.SharedKernel.
    private static global::Diaspora.SharedKernel.Results.Result<Conversation> Open(
        UserId? initiator = null, UserId? recipient = null, bool requiresAcceptance = false) =>
        Conversation.Open(
            ConversationId.New(),
            ConversationContext.Create(
                ConversationType.From(ConversationTypeKeys.MarketplaceListing),
                "listing-1",
                "Toyota Corolla 2018",
                Now),
            initiator ?? Buyer,
            recipient ?? Seller,
            Now,
            requiresAcceptance: requiresAcceptance);
}
