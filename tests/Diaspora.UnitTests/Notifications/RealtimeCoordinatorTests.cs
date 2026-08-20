using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Extensibility.Realtime;
using Diaspora.Modules.Notifications.Infrastructure.Realtime;
using Diaspora.Modules.Notifications.Options;
using Diaspora.Modules.Notifications.Realtime;
using Diaspora.Platform.Communication;
using Diaspora.SharedKernel.Identity;
using Diaspora.UnitTests.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diaspora.UnitTests.Notifications;

/// <summary>
/// The rules a hub applies, tested without a socket.
/// <para>
/// <b>Everything here is a refusal or a fan-out, and both fail silently in production.</b> A relay
/// that went to the wrong group tells the wrong people; a relay that was refused when it should not
/// have been leaves a client waiting for an event that never comes. Neither throws, and an
/// integration test would have to negotiate a transport before it could look.
/// </para>
/// </summary>
public class RealtimeCoordinatorTests
{
    private static readonly UserId Alice = UserId.From(Guid.Parse("11111111-1111-1111-1111-111111111111"));

    private readonly FakeClock _clock = new();
    private readonly InMemoryRealtimeConnectionRegistry _registry = new();
    private readonly RecordingRealtimeNotifier _notifier = new();

    [Fact]
    public async Task A_connection_is_acknowledged_with_what_the_deployment_supports()
    {
        var coordinator = Build();

        var handshake = await coordinator.OnConnectedAsync("c1", Alice, DevicePlatformKind.Android);

        handshake.Should().NotBeNull();
        handshake!.UserId.Should().Be(Alice.Value);
        handshake.ConnectionId.Should().Be("c1");
        handshake.PresenceEnabled.Should().BeTrue();
        handshake.TypingEnabled.Should().BeTrue();
        handshake.ReadReceiptsEnabled.Should().BeTrue();
        handshake.DeliveryReceiptsEnabled.Should().BeTrue();

        // This fixture builds the coordinator with only the member's own channel claimed, which is
        // what a deployment without the Conversations Platform has — and it correctly reports false.
        handshake.ConversationRelayEnabled.Should().BeFalse(
            "nothing in this deployment can say who is in a conversation, and a client must be told "
            + "rather than discovering it by joining a group and hearing nothing");
    }

    /// <summary>
    /// The other half of the flag, added by B13.2 and re-sourced by B13.3.
    /// <para>
    /// <b>The value follows what is installed, and nothing else.</b> It is not an option — an
    /// operator could set that true with nothing behind it, and every client would subscribe to
    /// groups that relay nothing. Until B13.3 it was an identity test against the shipped default,
    /// which was true exactly while one module could own the conversation family by replacing that
    /// default; with the prefix registry any number of modules contribute, so the honest source is
    /// whether anybody claimed <c>conversation:</c>.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_relay_is_announced_as_enabled_once_something_can_authorise_a_conversation()
    {
        var coordinator = Build(channels: [new PermissiveConversationChannelAuthorizer()]);

        var handshake = await coordinator.OnConnectedAsync("c1", Alice, DevicePlatformKind.Android);

        handshake!.ConversationRelayEnabled.Should().BeTrue();
        coordinator.ConversationRelayEnabled.Should().BeTrue(
            "the handshake and GET /realtime/configuration must read the same source, or a client "
            + "gates its typing UI on whichever it happened to read first");
    }

    /// <summary>
    /// A client stuck in a reconnect loop opens sockets until something stops it, and the thing it
    /// exhausts is the instance's file descriptors rather than its own.
    /// </summary>
    [Fact]
    public async Task A_member_over_the_connection_cap_is_refused()
    {
        var coordinator = Build(maxConnectionsPerUser: 2);

        (await coordinator.OnConnectedAsync("c1", Alice, DevicePlatformKind.IOS)).Should().NotBeNull();
        (await coordinator.OnConnectedAsync("c2", Alice, DevicePlatformKind.IOS)).Should().NotBeNull();
        (await coordinator.OnConnectedAsync("c3", Alice, DevicePlatformKind.IOS)).Should().BeNull();

        (await _registry.CountAsync()).Should().Be(2, "the refused socket was never registered");
    }

    [Fact]
    public async Task The_first_connection_announces_presence_and_the_second_does_not()
    {
        var coordinator = Build();

        await coordinator.OnConnectedAsync("phone", Alice, DevicePlatformKind.IOS);
        await coordinator.OnConnectedAsync("tablet", Alice, DevicePlatformKind.Tablet);

        _notifier.Sent
            .Count(sent => sent.EventName == RealtimeEvents.PresenceChanged)
            .Should().Be(1, "the second device is the same member, already online");
    }

    [Fact]
    public async Task The_last_disconnection_announces_offline()
    {
        var coordinator = Build();

        await coordinator.OnConnectedAsync("phone", Alice, DevicePlatformKind.IOS);
        await coordinator.OnConnectedAsync("tablet", Alice, DevicePlatformKind.Tablet);
        _notifier.Sent.Clear();

        await coordinator.OnDisconnectedAsync("phone", Alice);

        _notifier.Sent.Should().BeEmpty("one of two devices closing is not going offline");

        await coordinator.OnDisconnectedAsync("tablet", Alice);

        var presence = _notifier.Sent.Should().ContainSingle().Which;
        presence.EventName.Should().Be(RealtimeEvents.PresenceChanged);
        presence.Payload.Should().BeOfType<PresenceChanged>()
            .Which.Status.Should().Be(PresenceSnapshot.Offline);
    }

    /// <summary>
    /// The relay is authorised by the same seam a join is, so a client cannot skip the join and
    /// broadcast into a group it was never let into.
    /// </summary>
    [Fact]
    public async Task Typing_into_an_unauthorised_conversation_is_refused_and_sends_nothing()
    {
        var coordinator = Build();

        var relayed = await coordinator.RelayTypingAsync(
            Alice, "c1", Guid.NewGuid(), TypingSignal.Started);

        relayed.Should().BeFalse();
        _notifier.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Typing_reaches_the_group_when_the_authorizer_permits_it()
    {
        var conversationId = Guid.NewGuid();
        var coordinator = Build(channels: [new PermissiveConversationChannelAuthorizer()]);

        var relayed = await coordinator.RelayTypingAsync(
            Alice, "c1", conversationId, TypingSignal.Started);

        relayed.Should().BeTrue();

        var sent = _notifier.Sent.Should().ContainSingle().Which;
        sent.EventName.Should().Be(RealtimeEvents.TypingChanged);
        sent.Group.Should().Be(RealtimeGroups.ForConversation(conversationId));
        sent.ExceptConnectionId.Should().Be("c1", "a member is not told they are typing");
        sent.Payload.Should().BeOfType<TypingSignal>()
            .Which.EventType.Should().Be(TypingSignal.Started);
    }

    /// <summary>
    /// A stray value clears an indicator rather than pinning one on screen — the safe direction for
    /// a signal whose failure mode is "typing…" forever.
    /// </summary>
    [Theory]
    [InlineData("TypingStarted", "TypingStarted")]
    [InlineData("TypingUpdated", "TypingUpdated")]
    [InlineData("TypingEnded", "TypingEnded")]
    [InlineData("nonsense", "TypingEnded")]
    [InlineData(null, "TypingEnded")]
    public async Task An_unrecognised_typing_signal_becomes_ended(string? sent, string expected)
    {
        var coordinator = Build(channels: [new PermissiveConversationChannelAuthorizer()]);

        await coordinator.RelayTypingAsync(Alice, "c1", Guid.NewGuid(), sent!);

        _notifier.Sent.Should().ContainSingle().Which.Payload
            .Should().BeOfType<TypingSignal>()
            .Which.EventType.Should().Be(expected);
    }

    [Fact]
    public async Task A_read_receipt_names_the_authenticated_caller_rather_than_the_frame()
    {
        var conversationId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var coordinator = Build(channels: [new PermissiveConversationChannelAuthorizer()]);

        (await coordinator.RelayReadReceiptAsync(Alice, conversationId, messageId)).Should().BeTrue();

        var receipt = _notifier.Sent.Should().ContainSingle().Which.Payload
            .Should().BeOfType<ReadReceipt>().Which;

        receipt.ReadByUserId.Should().Be(Alice.Value);
        receipt.MessageId.Should().Be(messageId);
    }

    [Fact]
    public async Task A_delivery_receipt_is_distinct_from_a_read_receipt()
    {
        var coordinator = Build(channels: [new PermissiveConversationChannelAuthorizer()]);

        await coordinator.RelayDeliveryReceiptAsync(Alice, Guid.NewGuid(), Guid.NewGuid());

        _notifier.Sent.Should().ContainSingle().Which
            .EventName.Should().Be(RealtimeEvents.DeliveryReceiptReceived);
    }

    [Fact]
    public async Task Receipts_are_refused_when_the_deployment_has_switched_them_off()
    {
        var options = Options();
        options.Receipts.EnableReadReceipts = false;
        options.Receipts.EnableDeliveryReceipts = false;

        var coordinator = Build(options: options, channels: [new PermissiveConversationChannelAuthorizer()]);

        (await coordinator.RelayReadReceiptAsync(Alice, Guid.NewGuid(), Guid.NewGuid()))
            .Should().BeFalse();
        (await coordinator.RelayDeliveryReceiptAsync(Alice, Guid.NewGuid(), Guid.NewGuid()))
            .Should().BeFalse();

        _notifier.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task An_empty_identifier_is_refused_before_anything_is_authorised()
    {
        var coordinator = Build(channels: [new PermissiveConversationChannelAuthorizer()]);

        (await coordinator.RelayTypingAsync(Alice, "c1", Guid.Empty, TypingSignal.Started))
            .Should().BeFalse();
        (await coordinator.RelayReadReceiptAsync(Alice, Guid.NewGuid(), Guid.Empty))
            .Should().BeFalse();
    }

    /// <summary>
    /// A member the caller may not observe is omitted rather than reported offline: "offline" is a
    /// lie a client renders, and a per-identifier refusal would let a caller enumerate who exists.
    /// </summary>
    [Fact]
    public async Task Presence_for_members_the_caller_may_not_observe_is_omitted()
    {
        var bob = UserId.From(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var coordinator = Build();

        var snapshots = await coordinator.ObservePresenceAsync(Alice, [Alice, bob]);

        snapshots.Should().ContainSingle().Which.UserId.Should().Be(Alice);
    }

    private static RealtimeOptions Options() => new();

    /// <summary>
    /// Builds the coordinator over a channel registry, exactly as the container does.
    /// <para>
    /// The member's own channel is always claimed, because the runtime joins every socket to it on
    /// connect and a fixture without it would be testing a platform that cannot deliver a
    /// notification. Anything else a test wants is passed in.
    /// </para>
    /// </summary>
    private RealtimeConnectionCoordinator Build(
        int maxConnectionsPerUser = 10,
        RealtimeOptions? options = null,
        IRealtimeChannelAuthorizer[]? channels = null)
    {
        var resolved = options ?? Options();
        resolved.MaxConnectionsPerUser = maxConnectionsPerUser;

        var monitor = new StaticOptionsMonitor<RealtimeOptions>(resolved);

        var registry = new RealtimeChannelRegistry(
            [new UserRealtimeChannelAuthorizer(), .. channels ?? []],
            NullLogger<RealtimeChannelRegistry>.Instance);

        return new RealtimeConnectionCoordinator(
            _registry,
            new InMemoryPresenceStore(_registry, monitor, _clock),
            _notifier,
            new CompositeRealtimeGroupAuthorizer(registry),
            registry,
            monitor,
            _clock,
            NullLogger<RealtimeConnectionCoordinator>.Instance);
    }

    /// <summary>Stands in for a module that owns conversations and can say who is in one.</summary>
    private sealed class PermissiveConversationChannelAuthorizer : IRealtimeChannelAuthorizer
    {
        public string Module => "TestConversations";

        public string ChannelPrefix => RealtimeChannels.ConversationPrefix;

        public Task<bool> CanJoinAsync(
            UserId userId, string channel, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> CanObservePresenceAsync(
            UserId observer, UserId subject, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private sealed record SentFrame(
        string EventName, object Payload, string? Group, string? ExceptConnectionId);

    /// <summary>
    /// The transport, recorded instead of sent. Everything above <see cref="IRealtimeNotifier"/> is
    /// transport-free by design, which is exactly what makes this substitution a two-line class.
    /// </summary>
    private sealed class RecordingRealtimeNotifier : IRealtimeNotifier
    {
        public List<SentFrame> Sent { get; } = [];

        public bool IsEnabled => true;

        public Task SendToUserAsync(
            UserId userId, string eventName, object payload,
            CancellationToken cancellationToken = default)
        {
            Sent.Add(new SentFrame(eventName, payload, RealtimeGroups.ForUser(userId), null));

            return Task.CompletedTask;
        }

        public Task SendToGroupAsync(
            string group, string eventName, object payload,
            CancellationToken cancellationToken = default)
        {
            Sent.Add(new SentFrame(eventName, payload, group, null));

            return Task.CompletedTask;
        }

        public Task SendToGroupExceptAsync(
            string group, string exceptConnectionId, string eventName, object payload,
            CancellationToken cancellationToken = default)
        {
            Sent.Add(new SentFrame(eventName, payload, group, exceptConnectionId));

            return Task.CompletedTask;
        }
    }
}
