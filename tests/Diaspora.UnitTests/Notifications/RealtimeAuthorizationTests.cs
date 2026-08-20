using System.Reflection;
using Diaspora.Modules.Notifications.Extensibility.Realtime;
using Diaspora.Modules.Notifications.Infrastructure.Realtime;
using Diaspora.Modules.Notifications.Realtime;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Notifications;

/// <summary>
/// Who may join what, and what the client contract says.
/// <para>
/// <b>A group name is an authorisation boundary supplied by the client.</b> Everything a hub relays
/// — typing, read receipts, delivery acknowledgements — goes to a group, so a runtime that accepted
/// whatever name it was handed would leak all three to anybody who guessed a conversation
/// identifier. These tests are the check that the shipped authorizer refuses, and that it refuses
/// for the right reason rather than because a string comparison happened to fail.
/// </para>
/// </summary>
public class RealtimeAuthorizationTests
{
    private static readonly UserId Alice = UserId.From(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly UserId Bob = UserId.From(Guid.Parse("22222222-2222-2222-2222-222222222222"));

    private readonly OwnGroupRealtimeGroupAuthorizer _authorizer = new();

    [Fact]
    public async Task A_member_may_join_their_own_group()
    {
        (await _authorizer.CanJoinAsync(Alice, RealtimeGroups.ForUser(Alice)))
            .Should().BeTrue();
    }

    [Fact]
    public async Task A_member_may_not_join_somebody_elses_group()
    {
        (await _authorizer.CanJoinAsync(Alice, RealtimeGroups.ForUser(Bob)))
            .Should().BeFalse("that group carries Bob's notifications");
    }

    /// <summary>
    /// The finding this authorizer exists for. No module owns conversations, so nothing can say who
    /// is in one — and a runtime that relayed anyway would be relaying to whoever asked.
    /// </summary>
    [Fact]
    public async Task Nobody_may_join_a_conversation_group()
    {
        var group = RealtimeGroups.ForConversation(Guid.NewGuid());

        (await _authorizer.CanJoinAsync(Alice, group)).Should().BeFalse();
    }

    /// <summary>
    /// A client supplies the name, and "starts with user:" is satisfied by <c>user:</c> followed by
    /// anything at all — including another member's identifier with a character appended.
    /// </summary>
    [Theory]
    [InlineData("user:")]
    [InlineData("user:not-a-guid")]
    [InlineData("user:00000000-0000-0000-0000-000000000000")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("everybody")]
    public async Task A_malformed_group_name_is_refused(string group)
    {
        (await _authorizer.CanJoinAsync(Alice, group)).Should().BeFalse();
    }

    [Fact]
    public async Task A_member_may_observe_only_their_own_presence()
    {
        (await _authorizer.CanObservePresenceAsync(Alice, Alice)).Should().BeTrue();
        (await _authorizer.CanObservePresenceAsync(Alice, Bob)).Should().BeFalse();
    }

    [Fact]
    public void A_user_group_round_trips_to_the_member_it_names()
    {
        RealtimeGroups.UserOf(RealtimeGroups.ForUser(Alice)).Should().Be(Alice);
        RealtimeGroups.UserOf(RealtimeGroups.ForConversation(Guid.NewGuid())).Should().BeNull();
    }

    /// <summary>
    /// SignalR dispatches to a client by method name, so <see cref="IRealtimeClient"/> and
    /// <see cref="RealtimeEvents"/> are two spellings of one contract. Nothing else would notice them
    /// drifting: renaming a method compiles, renaming a constant compiles, and the first symptom is
    /// a shipped mobile build that stops receiving one kind of event.
    /// </summary>
    [Fact]
    public void Every_client_method_is_a_declared_event_name()
    {
        var declared = typeof(RealtimeEvents)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToHashSet(StringComparer.Ordinal);

        var methods = typeof(IRealtimeClient)
            .GetMethods()
            .Select(method => method.Name)
            .ToList();

        methods.Should().NotBeEmpty();
        methods.Should().OnlyContain(
            name => declared.Contains(name),
            "a client method whose name is not in RealtimeEvents is an event nothing can fan out to");
    }

    /// <summary>
    /// The lower-case spellings are what a client renders. Serialising the enum instead would make a
    /// C# rename a silent breaking change on the wire.
    /// </summary>
    [Theory]
    [InlineData(PresenceState.Online, "online")]
    [InlineData(PresenceState.Away, "away")]
    [InlineData(PresenceState.Offline, "offline")]
    public void Presence_is_spelled_the_way_the_client_reads_it(PresenceState state, string expected)
    {
        PresenceSnapshot.Spell(state).Should().Be(expected);
    }
}
