using Diaspora.Contracts.Conversations.V1.Events;
using Diaspora.Modules.Conversations.Application.EventHandlers;
using Diaspora.Modules.Conversations.Domain.Conversations;
using Diaspora.Modules.Conversations.Domain.Enums;
using Diaspora.Modules.Conversations.Domain.Events;
using Diaspora.Modules.Conversations.Domain.Identifiers;
using Diaspora.Modules.Conversations.Domain.Messages;
using Diaspora.Modules.Conversations.Domain.ValueObjects;
using Diaspora.Platform.Messaging;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Conversations;

/// <summary>
/// A conversation's first message, announced as a fact of its own — 13 Sep 2026.
/// <para>
/// <b>Why it exists.</b> Tapping "Message" on a listing opens a conversation before anything is typed,
/// and two of the first three enquiry conversations in production were never written in. Analytics
/// counts an enquiry when a conversation gets its first message, so the first message has to be told
/// apart from every later one — and told whether or not anybody is notified about it, which the
/// notification event cannot do.
/// </para>
/// </summary>
public sealed class FirstMessageTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 13, 10, 0, 0, TimeSpan.Zero);

    private static readonly UserId Buyer = UserId.From(Guid.Parse("33333333-3333-3333-3333-333333333333"));
    private static readonly UserId Seller = UserId.From(Guid.Parse("44444444-4444-4444-4444-444444444444"));

    [Fact]
    public void A_conversations_count_is_one_exactly_when_its_first_message_is_recorded()
    {
        // The rule both send paths read: "MessageCount == 1 once this message is recorded".
        var conversation = Open();

        conversation.MessageCount.Should().Be(0, "opening a conversation writes nothing");

        conversation.RecordMessage(ConversationMessageId.New(), Buyer, Now).IsSuccess.Should().BeTrue();
        conversation.MessageCount.Should().Be(1);

        conversation.RecordMessage(ConversationMessageId.New(), Seller, Now).IsSuccess.Should().BeTrue();
        conversation.MessageCount.Should().Be(2);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_sent_event_carries_whether_the_message_opened_the_conversation(bool opens)
    {
        var message = ConversationMessage.Send(
            ConversationMessageId.New(), ConversationId.New(), Buyer, "Is it still available?", Now).Value;

        message.DeclareRecipients([Seller], opensConversation: opens);

        var sent = message.DomainEvents.OfType<MessageSentDomainEvent>().Should().ContainSingle().Subject;

        sent.OpensConversation.Should().Be(opens);
        sent.RecipientUserIds.Should().Equal(Seller);
    }

    [Fact]
    public async Task The_first_message_is_announced_with_what_the_conversation_is_about()
    {
        var publisher = Substitute.For<IIntegrationEventPublisher>();
        var relay = new ConversationIntegrationEventRelay(
            publisher, NullLogger<ConversationIntegrationEventRelay>.Instance);

        var conversation = Open();
        var messageId = ConversationMessageId.New();

        await relay.PublishFirstMessageAsync(Sent(conversation, messageId, opens: true), conversation);

        var call = publisher.ReceivedCalls().Should().ContainSingle().Subject;
        var published = call.GetArguments()[0].Should()
            .BeOfType<ConversationFirstMessageSentIntegrationEvent>().Subject;

        published.ConversationId.Should().Be(conversation.Id.Value);
        published.MessageId.Should().Be(messageId.Value);
        published.SenderUserId.Should().Be(Buyer.Value);
        published.ContextType.Should().Be("MarketplaceListing");
        published.EntityId.Should().Be("listing-1", "the entity id is what analytics attributes the enquiry to");
        published.SentAt.Should().Be(Now);
    }

    [Fact]
    public async Task A_later_message_is_not_announced()
    {
        var publisher = Substitute.For<IIntegrationEventPublisher>();
        var relay = new ConversationIntegrationEventRelay(
            publisher, NullLogger<ConversationIntegrationEventRelay>.Instance);

        var conversation = Open();

        await relay.PublishFirstMessageAsync(Sent(conversation, ConversationMessageId.New(), opens: false), conversation);

        await publisher.DidNotReceiveWithAnyArgs().PublishAsync(default!, default);
    }

    private static MessageSentDomainEvent Sent(Conversation conversation, ConversationMessageId messageId, bool opens) =>
        new()
        {
            ConversationId = conversation.Id,
            MessageId = messageId,
            SenderId = Buyer,
            Kind = MessageKind.Text,
            RecipientUserIds = [Seller],
            SentAt = Now,
            OpensConversation = opens
        };

    private static Conversation Open() =>
        Conversation.Open(
            ConversationId.New(),
            ConversationContext.Create(
                ConversationType.From(ConversationTypeKeys.MarketplaceListing),
                "listing-1",
                "Toyota Corolla 2018",
                Now),
            Buyer,
            Seller,
            Now).Value;
}
