using Diaspora.Modules.Conversations.Application.Mapping;
using Diaspora.Modules.Conversations.Domain.Conversations;
using Diaspora.Modules.Conversations.Domain.Enums;
using Diaspora.Modules.Conversations.Domain.Errors;
using Diaspora.Modules.Conversations.Domain.Identifiers;
using Diaspora.Modules.Conversations.Domain.Messages;
using Diaspora.Modules.Conversations.Domain.Policies;
using Diaspora.Modules.Conversations.Domain.ValueObjects;
using Diaspora.Modules.Conversations.Persistence;
using Diaspora.Persistence.Paging;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Conversations;

/// <summary>The message aggregate, the state policy, the wire names and the cursor.</summary>
public sealed class MessageAndStateTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 6, 10, 0, 0, TimeSpan.Zero);

    private static readonly UserId Sender = UserId.From(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly UserId Other = UserId.From(Guid.Parse("22222222-2222-2222-2222-222222222222"));

    // ── Message lifecycle ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_message_with_neither_text_nor_an_attachment_is_refused()
    {
        var result = ConversationMessage.Send(
            ConversationMessageId.New(), ConversationId.New(), Sender, "   ", Now);

        result.Error.Should().Be(ConversationErrors.EmptyMessage);
    }

    [Fact]
    public void An_attachment_decides_the_kind_when_the_caller_names_none()
    {
        var message = ConversationMessage.Send(
            ConversationMessageId.New(),
            ConversationId.New(),
            Sender,
            body: null,
            Now,
            attachment: Attachment(AttachmentKind.Image)).Value;

        message.Kind.Should().Be(MessageKind.Image);
    }

    /// <summary>A member must not be able to forge the platform's own voice.</summary>
    [Fact]
    public void A_member_cannot_send_a_system_message()
    {
        var message = ConversationMessage.Send(
            ConversationMessageId.New(),
            ConversationId.New(),
            Sender,
            "I am the platform",
            Now,
            MessageKind.System).Value;

        message.Kind.Should().Be(MessageKind.Text);
        message.IsSystem.Should().BeFalse();
    }

    [Fact]
    public void A_system_message_has_no_sender_and_cannot_be_edited_or_deleted()
    {
        var message = ConversationMessage.System(
            ConversationMessageId.New(),
            ConversationId.New(),
            SystemMessageKind.ConversationStarted,
            "Conversation started",
            Now);

        message.SenderId.Should().BeNull();
        message.IsSystem.Should().BeTrue();
        message.Edit(Sender, "no", Now).Error.Should().Be(ConversationErrors.SystemMessageImmutable);
        message.Delete(Sender, Now).Error.Should().Be(ConversationErrors.SystemMessageImmutable);
    }

    [Fact]
    public void Only_the_sender_can_edit_and_only_within_the_window()
    {
        var message = Text();

        message.Edit(Other, "changed", Now).Error.Should().Be(ConversationErrors.NotTheSender);

        message.Edit(Sender, "changed", Now + ConversationLimits.EditWindow.Add(TimeSpan.FromMinutes(1)))
            .Error.Should().Be(ConversationErrors.EditWindowExpired);

        message.Edit(Sender, "changed", Now.AddMinutes(1)).IsSuccess.Should().BeTrue();
        message.Body.Should().Be("changed");
        message.IsEdited.Should().BeTrue();
    }

    [Fact]
    public void Only_text_messages_can_be_edited()
    {
        var message = ConversationMessage.Send(
            ConversationMessageId.New(),
            ConversationId.New(),
            Sender,
            "look",
            Now,
            attachment: Attachment(AttachmentKind.Image)).Value;

        message.Edit(Sender, "different", Now).Error
            .Should().Be(ConversationErrors.MessageNotEditable);
    }

    /// <summary>
    /// Deleting must remove what was said, not merely its rendering. A body still in the table is a
    /// body still in every export.
    /// </summary>
    [Fact]
    public void Deleting_clears_the_body_and_the_attachment_but_keeps_the_row()
    {
        var message = ConversationMessage.Send(
            ConversationMessageId.New(),
            ConversationId.New(),
            Sender,
            "regrettable",
            Now,
            attachment: Attachment(AttachmentKind.Image)).Value;

        message.Delete(Sender, Now.AddMinutes(1)).IsSuccess.Should().BeTrue();

        message.Body.Should().BeNull();
        message.Attachment.Should().BeNull();
        message.IsWithdrawn.Should().BeTrue();
        message.Status.Should().Be(MessageStatus.Deleted);
    }

    [Fact]
    public void A_moderator_deletion_is_hidden_rather_than_deleted()
    {
        var message = Text();

        message.Delete(Other, Now, asModerator: true).IsSuccess.Should().BeTrue();

        message.Status.Should().Be(
            MessageStatus.Hidden, "the sender did not choose this and the distinction is auditable");
    }

    /// <summary>
    /// Latching. A member with three devices should not watch the tick time drift as each wakes up.
    /// </summary>
    [Fact]
    public void Delivery_and_read_timestamps_latch_on_the_first_acknowledgement()
    {
        var message = Text();

        message.MarkDelivered(Now.AddMinutes(1));
        message.MarkDelivered(Now.AddMinutes(5));

        message.DeliveredAt.Should().Be(Now.AddMinutes(1));

        message.MarkRead(Now.AddMinutes(2));
        message.MarkRead(Now.AddMinutes(9));

        message.ReadAt.Should().Be(Now.AddMinutes(2));
        message.Status.Should().Be(MessageStatus.Read);
    }

    [Fact]
    public void A_withdrawn_message_does_not_acquire_receipts()
    {
        var message = Text();

        message.Delete(Sender, Now).IsSuccess.Should().BeTrue();
        message.MarkDelivered(Now.AddMinutes(1));
        message.MarkRead(Now.AddMinutes(2));

        message.DeliveredAt.Should().BeNull();
        message.ReadAt.Should().BeNull();
        message.Status.Should().Be(MessageStatus.Deleted);
    }

    // ── State policy: the storage-to-wire reconciliation ─────────────────────────────────────────

    /// <summary>
    /// The precedence that keeps the composer honest. Blocked outranks everything because the client
    /// disables its text box on that signal alone.
    /// </summary>
    [Fact]
    public void Blocked_outranks_archived_and_muted_when_resolving_the_state()
    {
        var conversation = Conversation.Open(
            ConversationId.New(), Context(), Sender, Other, Now).Value;

        conversation.Archive(Other, Now);
        conversation.Mute(Other, null, Now);

        ConversationStatePolicy
            .Resolve(conversation, conversation.ParticipantFor(Other), isBlocked: true, Now)
            .Should().Be(ConversationState.Blocked);

        ConversationStatePolicy
            .Resolve(conversation, conversation.ParticipantFor(Other), isBlocked: false, Now)
            .Should().Be(ConversationState.Archived, "archiving is the stronger filing decision");
    }

    /// <summary>
    /// The same conversation is Archived for one participant and Active for the other. This is the
    /// case a single stored state field cannot represent.
    /// </summary>
    [Fact]
    public void The_state_differs_per_viewer()
    {
        var conversation = Conversation.Open(
            ConversationId.New(), Context(), Sender, Other, Now).Value;

        conversation.Archive(Other, Now);

        ConversationStatePolicy
            .Resolve(conversation, conversation.ParticipantFor(Other), false, Now)
            .Should().Be(ConversationState.Archived);

        ConversationStatePolicy
            .Resolve(conversation, conversation.ParticipantFor(Sender), false, Now)
            .Should().Be(ConversationState.Active);
    }

    /// <summary>
    /// A pending request looks different from each end: the initiator opened it, the recipient owes
    /// an answer.
    /// </summary>
    [Fact]
    public void A_pending_request_reads_as_accepted_to_its_initiator()
    {
        var conversation = Conversation.Open(
            ConversationId.New(), Context(), Sender, Other, Now, requiresAcceptance: true).Value;

        ConversationStatePolicy
            .Resolve(conversation, conversation.ParticipantFor(Sender), false, Now)
            .Should().Be(ConversationState.Accepted);

        ConversationStatePolicy
            .Resolve(conversation, conversation.ParticipantFor(Other), false, Now)
            .Should().Be(ConversationState.PendingRequest);
    }

    [Fact]
    public void An_expired_mute_no_longer_reads_as_muted()
    {
        var conversation = Conversation.Open(
            ConversationId.New(), Context(), Sender, Other, Now).Value;

        conversation.Mute(Other, Now.AddHours(8), Now);

        ConversationStatePolicy
            .Resolve(conversation, conversation.ParticipantFor(Other), false, Now.AddHours(9))
            .Should().Be(ConversationState.Active);
    }

    [Fact]
    public void CanSend_refuses_a_blocked_or_closed_conversation_and_a_spent_request()
    {
        var conversation = Conversation.Open(
            ConversationId.New(), Context(), Sender, Other, Now, requiresAcceptance: true).Value;

        ConversationStatePolicy
            .CanSend(conversation, conversation.ParticipantFor(Sender), isBlocked: true)
            .Should().BeFalse();

        ConversationStatePolicy
            .CanSend(conversation, conversation.ParticipantFor(Sender), isBlocked: false)
            .Should().BeTrue("the opening message has not been sent yet");

        conversation.RecordMessage(ConversationMessageId.New(), Sender, Now);

        ConversationStatePolicy
            .CanSend(conversation, conversation.ParticipantFor(Sender), isBlocked: false)
            .Should().BeFalse("the initiator has used their one message into an unaccepted request");
    }

    // ── Wire names ──────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(MessageKind.Text, "text")]
    [InlineData(MessageKind.Image, "image")]
    [InlineData(MessageKind.Voice, "voice")]
    [InlineData(MessageKind.Document, "document")]
    [InlineData(MessageKind.System, "system")]
    [InlineData(MessageKind.Verification, "verification")]
    public void Message_kinds_are_spelled_the_way_the_client_spells_them(
        MessageKind kind, string expected) =>
        ConversationWireNames.ToWire(kind).Should().Be(expected);

    [Theory]
    [InlineData(ConversationState.PendingRequest, "PendingRequest")]
    [InlineData(ConversationState.Accepted, "Accepted")]
    [InlineData(ConversationState.Active, "Active")]
    [InlineData(ConversationState.Muted, "Muted")]
    [InlineData(ConversationState.Archived, "Archived")]
    [InlineData(ConversationState.Blocked, "Blocked")]
    [InlineData(ConversationState.Closed, "Closed")]
    public void Conversation_states_keep_the_client_casing(
        ConversationState state, string expected) =>
        ConversationWireNames.ToWire(state).Should().Be(expected);

    /// <summary>
    /// A withdrawn message keeps its last real delivery state. The client has no "deleted" status —
    /// deletion is its own boolean, which it already renders.
    /// </summary>
    [Fact]
    public void A_withdrawn_message_does_not_invent_a_seventh_wire_status() =>
        ConversationWireNames.ToWire(MessageStatus.Deleted).Should().Be("sent");

    [Theory]
    [InlineData("8h", MuteDuration.EightHours)]
    [InlineData("24h", MuteDuration.OneDay)]
    [InlineData("7d", MuteDuration.SevenDays)]
    [InlineData("forever", MuteDuration.Forever)]
    public void The_four_client_mute_literals_parse(string literal, MuteDuration expected) =>
        ConversationWireNames.ParseMuteDuration(literal).Should().Be(expected);

    /// <summary>Null is an unmute — it is what the client sends from its unmute path.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nonsense")]
    public void Anything_else_is_an_unmute(string? literal) =>
        ConversationWireNames.ParseMuteDuration(literal).Should().BeNull();

    [Fact]
    public void Forever_has_no_end_and_the_others_do()
    {
        ConversationWireNames.MutedUntil(MuteDuration.Forever, Now).Should().BeNull();
        ConversationWireNames.MutedUntil(MuteDuration.EightHours, Now).Should().Be(Now.AddHours(8));
        ConversationWireNames.MutedUntil(MuteDuration.SevenDays, Now).Should().Be(Now.AddDays(7));
    }

    // ── Cursor ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_cursor_round_trips_its_boundary()
    {
        var encoded = ConversationCursor.Create(Now, "abc,def").Encode();

        var parsed = ConversationCursor.Parse(encoded);

        parsed.Should().NotBeNull();
        parsed!.Timestamp.Should().Be(Now);
        parsed.BoundaryKey.Should().Be("abc,def");
    }

    /// <summary>
    /// An unreadable cursor is no cursor. A 400 mid-scroll is worse than a first page.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-base64!!")]
    [InlineData("bm90LWEtY3Vyc29y")]
    public void An_unreadable_cursor_is_treated_as_absent(string? token) =>
        ConversationCursor.Parse(token).Should().BeNull();

    /// <summary>
    /// The tie-break the analyzer forced. Two messages in one millisecond is the ordinary case here.
    /// </summary>
    [Fact]
    public void The_boundary_carries_every_identifier_sharing_the_last_sort_key()
    {
        var rows = new[]
        {
            (Sent: Now.AddMinutes(1), Id: Guid.NewGuid()),
            (Sent: Now, Id: Guid.NewGuid()),
            (Sent: Now, Id: Guid.NewGuid())
        };

        var key = CursorBoundary.Key(rows, row => row.Sent, row => row.Id);

        var exclusion = CursorBoundary.Exclusion(key);

        exclusion.Ids.Should().HaveCount(2, "both rows at the boundary instant must be excluded");
        exclusion.IsExhausted.Should().BeFalse();
    }

    private static ConversationMessage Text() =>
        ConversationMessage.Send(
            ConversationMessageId.New(), ConversationId.New(), Sender, "hello", Now).Value;

    private static AttachmentReference Attachment(AttachmentKind kind) =>
        AttachmentReference.Create(
            ConversationAttachmentId.New(), kind, Guid.NewGuid());

    private static ConversationContext Context() =>
        ConversationContext.Create(
            ConversationType.From(ConversationTypeKeys.MarketplaceListing),
            "listing-1",
            "Toyota Corolla 2018",
            Now);
}
