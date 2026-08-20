using Diaspora.Modules.Notifications.Infrastructure.Realtime;
using Diaspora.Platform.Communication;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diaspora.UnitTests.Notifications;

/// <summary>
/// The composition B13.3 replaced a single-winner seam with.
/// <para>
/// <b>Every test here is about a failure that produces no error anywhere.</b> A channel nobody
/// claimed refuses silently and looks like a member with no permission. A second module claiming a
/// prefix silently switches off the first — which is what actually happened to the design this
/// registry replaced, and would have taken a bug report from somebody whose chat stopped working to
/// find. A contributor whose database is unreachable must answer "no" rather than throw, or an
/// outage becomes an error frame a client cannot tell from a bug.
/// </para>
/// </summary>
public class RealtimeChannelRegistryTests
{
    private static readonly UserId Alice =
        UserId.From(Guid.Parse("11111111-1111-1111-1111-111111111111"));

    private static readonly UserId Bob =
        UserId.From(Guid.Parse("22222222-2222-2222-2222-222222222222"));

    [Fact]
    public async Task A_member_may_join_their_own_channel_and_not_somebody_elses()
    {
        var registry = Build();

        (await registry.CanJoinAsync(Alice, RealtimeChannels.ForUser(Alice))).Should().BeTrue();
        (await registry.CanJoinAsync(Alice, RealtimeChannels.ForUser(Bob))).Should().BeFalse(
            "that channel carries Bob's notifications");
    }

    /// <summary>
    /// The shipped answer for every name a client can invent. A hub accepts an arbitrary string from
    /// a socket, and this is what stops the string being able to do anything.
    /// </summary>
    [Theory]
    [InlineData("conversation:4f21e1d0-0000-4000-8000-000000000001")]
    [InlineData("organization:4f21e1d0-0000-4000-8000-000000000001")]
    [InlineData("everybody")]
    [InlineData("admin:all")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_channel_family_nobody_claimed_is_refused(string channel)
    {
        (await Build().CanJoinAsync(Alice, channel)).Should().BeFalse();
    }

    /// <summary>
    /// A client supplies the name, and "starts with user:" is satisfied by <c>user:</c> followed by
    /// anything at all — including another member's identifier with a character appended.
    /// </summary>
    [Theory]
    [InlineData("user:")]
    [InlineData("user:not-a-guid")]
    [InlineData("user:00000000-0000-0000-0000-000000000000")]
    public async Task A_malformed_channel_name_is_refused(string channel)
    {
        (await Build().CanJoinAsync(Alice, channel)).Should().BeFalse();
    }

    /// <summary>
    /// <b>The whole reason this type exists.</b> The design it replaced let a second module register
    /// an <c>IRealtimeGroupAuthorizer</c> and silently take over from the first, which would have
    /// stopped every conversation on the platform relaying with nothing logged and no test failing.
    /// </summary>
    [Fact]
    public void Two_modules_claiming_one_prefix_is_a_startup_failure()
    {
        var build = () => new RealtimeChannelRegistry(
            [
                new StubChannelAuthorizer("conversation:", "Conversations"),
                new StubChannelAuthorizer("conversation:", "SomethingElse")
            ],
            NullLogger<RealtimeChannelRegistry>.Instance);

        build.Should().Throw<InvalidOperationException>()
            .WithMessage("*Conversations*SomethingElse*");
    }

    /// <summary>
    /// A prefix of <c>event</c> would also claim <c>eventual:</c>. Refused at construction, because
    /// the overlap only surfaces once a second module picks a name starting with the same word.
    /// </summary>
    [Theory]
    [InlineData("event")]
    [InlineData(":")]
    [InlineData("")]
    public void A_prefix_without_its_separator_is_a_startup_failure(string prefix)
    {
        var build = () => new RealtimeChannelRegistry(
            [new StubChannelAuthorizer(prefix, "Broken")],
            NullLogger<RealtimeChannelRegistry>.Instance);

        build.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// Joining is one channel's question, so one contributor answers it. Presence is a question about
    /// a person — sharing a conversation and sharing an organization are independent reasons to be
    /// allowed to see somebody's dot — so every contributor is asked and the first yes wins.
    /// </summary>
    [Fact]
    public async Task Presence_is_unioned_across_contributors_while_joining_is_not()
    {
        var registry = Build(new StubChannelAuthorizer("conversation:", "Conversations")
        {
            PermitsPresence = true
        });

        (await registry.CanObservePresenceAsync(Alice, Bob)).Should().BeTrue(
            "one contributor permits it, and no prefix owns a question about a person");

        (await registry.CanJoinAsync(Alice, RealtimeChannels.ForUser(Bob))).Should().BeFalse(
            "the user family's owner refused, and no other contributor gets a say");
    }

    /// <summary>
    /// A contributor reads a table and a table can be unreachable. Propagating would turn a transient
    /// fault into an error frame a client cannot tell apart from a bug, on a path whose only correct
    /// failure mode is "no".
    /// </summary>
    [Fact]
    public async Task A_contributor_that_throws_is_treated_as_a_refusal()
    {
        var registry = Build(new StubChannelAuthorizer("conversation:", "Broken") { Throws = true });

        var channel = RealtimeChannels.ForConversation(Guid.NewGuid());

        (await registry.CanJoinAsync(Alice, channel)).Should().BeFalse();
        (await registry.ListSubscribersAsync(channel, 10)).Should().BeEmpty();
        (await registry.CanObservePresenceAsync(Alice, Bob)).Should().BeFalse();
    }

    /// <summary>
    /// The caller's cap is enforced here as well as by the contributor, because the contract says the
    /// list is bounded and a contributor is somebody else's code.
    /// </summary>
    [Fact]
    public async Task A_subscriber_list_longer_than_the_caller_asked_for_is_truncated()
    {
        var generous = new StubChannelAuthorizer("conversation:", "Conversations")
        {
            Subscribers = [Alice, Bob, UserId.From(Guid.NewGuid())]
        };

        var subscribers = await Build(generous).ListSubscribersAsync(
            RealtimeChannels.ForConversation(Guid.NewGuid()), 2);

        subscribers.Should().HaveCount(2);
    }

    [Fact]
    public void The_owned_prefixes_are_reported_for_the_handshake_and_the_health_check()
    {
        var registry = Build(new StubChannelAuthorizer("conversation:", "Conversations"));

        registry.IsOwned(RealtimeChannels.ConversationPrefix).Should().BeTrue();
        registry.IsOwned(RealtimeChannels.OrganizationPrefix).Should().BeFalse();
        registry.OwnedPrefixes.Should().BeEquivalentTo(["user:", "conversation:"]);
    }

    private static RealtimeChannelRegistry Build(params IRealtimeChannelAuthorizer[] extra) =>
        new(
            [new UserRealtimeChannelAuthorizer(), .. extra],
            NullLogger<RealtimeChannelRegistry>.Instance);

    private sealed class StubChannelAuthorizer : IRealtimeChannelAuthorizer
    {
        public StubChannelAuthorizer(string prefix, string module)
        {
            ChannelPrefix = prefix;
            Module = module;
        }

        public string Module { get; }

        public string ChannelPrefix { get; }

        public bool PermitsPresence { get; init; }

        public bool Throws { get; init; }

        public IReadOnlyList<UserId> Subscribers { get; init; } = [];

        public Task<bool> CanJoinAsync(
            UserId userId, string channel, CancellationToken cancellationToken = default) =>
            Throws ? throw new InvalidOperationException("unreachable") : Task.FromResult(true);

        public Task<IReadOnlyList<UserId>> ListSubscribersAsync(
            string channel, int limit, CancellationToken cancellationToken = default) =>
            Throws ? throw new InvalidOperationException("unreachable") : Task.FromResult(Subscribers);

        public Task<bool> CanObservePresenceAsync(
            UserId observer, UserId subject, CancellationToken cancellationToken = default) =>
            Throws
                ? throw new InvalidOperationException("unreachable")
                : Task.FromResult(PermitsPresence);
    }
}
