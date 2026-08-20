using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Notifications.Extensibility.Realtime;
using Diaspora.Modules.Notifications.Infrastructure.Realtime;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// The real-time runtime, driven the way a phone drives it.
/// <para>
/// <b>Through the real SignalR client rather than by calling hub methods on a constructed
/// instance.</b> A hub exercised by construction proves the C# compiles. It proves nothing about
/// the three things that actually break: whether a bearer token survives the transport upgrade,
/// whether the user identifier resolves from the claim Identity actually issues, and whether a
/// payload serialises into the shape a client binds to. Each of those fails silently — no
/// exception, no log above Debug, one member at a time.
/// </para>
/// <para>
/// Long polling rather than WebSockets, because <c>TestServer</c> speaks HTTP and the negotiate,
/// authentication, dispatch and serialisation paths under test are identical on both. The one thing
/// long polling does <em>not</em> exercise — a token arriving in the query string, which is the only
/// way a browser or React Native socket can authenticate — is covered separately by driving the
/// negotiate endpoint directly.
/// </para>
/// </summary>
public sealed class CommunicationRuntimeTests : IdentityTestBase<RealtimeWebApplicationFactory>
{
    private const string Root = "/api/v1/notifications";
    private const string HubPath = "/hubs/realtime";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public CommunicationRuntimeTests(RealtimeWebApplicationFactory factory) : base(factory) { }

    // ── Composition ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The runtime composed in through contributor interfaces alone.</b> <c>AddSignalR</c> is
    /// called by the module, the hub is mapped by the module's endpoint contributor, and
    /// <c>Program.cs</c> names none of it. A resolvable notifier and a mapped hub together are the
    /// proof.
    /// </summary>
    [Fact]
    public void The_realtime_runtime_is_registered_by_the_module()
    {
        using var scope = Factory.Services.CreateScope();

        scope.ServiceProvider.GetService<IRealtimeNotifier>().Should().NotBeNull();
        scope.ServiceProvider.GetService<IRealtimeConnectionRegistry>().Should().NotBeNull();
        scope.ServiceProvider.GetService<IPresenceStore>().Should().NotBeNull();
        scope.ServiceProvider.GetService<IRealtimeBackplane>().Should().NotBeNull();
        scope.ServiceProvider.GetService<IRealtimeGroupAuthorizer>().Should().NotBeNull();
    }

    [Fact]
    public async Task The_runtime_configuration_is_served_to_a_client()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync(
            new Uri($"{Root}/realtime/configuration", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var configuration = await ReadDataAsync<RealtimeConfigurationShape>(response);

        configuration.Enabled.Should().BeTrue();
        configuration.HubPath.Should().Be(HubPath);
        configuration.PresenceEnabled.Should().BeTrue();
        configuration.TypingEnabled.Should().BeTrue();
        configuration.ReadReceiptsEnabled.Should().BeTrue();
        configuration.DeliveryReceiptsEnabled.Should().BeTrue();

        // B13.2 inverted this assertion, and the inversion is the phase's whole point. It read
        // "false, because no module owns conversations, so nothing can say who is in one". That
        // premise held for exactly as long as no conversations vertical existed. The Conversations
        // Platform registers its own IRealtimeGroupAuthorizer at order 1500, which replaces the
        // shipped own-group default, and the flag follows the registration rather than a constant.
        //
        // The client contract is unchanged: it still reads one boolean and gates its typing and
        // receipt UI on it. What changed is the answer.
        configuration.ConversationRelayEnabled.Should().BeTrue(
            "the Conversations Platform is deployed and can say who is in a conversation. A false "
            + "here would mean its group authorizer failed to replace the own-group default — which "
            + "nothing else reports, because the symptom is a relay that silently reaches nobody");
    }

    // ── Authentication ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// An unauthenticated socket is refused at negotiate, before it is accepted and counted.
    /// </summary>
    [Fact]
    public async Task The_hub_refuses_an_anonymous_connection()
    {
        using var anonymous = Factory.CreateClient();

        var response = await anonymous.PostAsync(
            new Uri($"{HubPath}/negotiate?negotiateVersion=1", UriKind.Relative), null);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// <b>The check that the host change exists for.</b> Neither a browser's WebSocket nor React
    /// Native's lets a caller set an Authorization header on the upgrade, so every SignalR client
    /// authenticates by query string. Without the host accepting it there, the hub is unreachable to
    /// every client that is not long-polling — and the symptom is a 401 on negotiate that reads
    /// exactly like an expired token.
    /// </summary>
    [Fact]
    public async Task A_bearer_token_in_the_query_string_authenticates_the_hub()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        using var anonymous = Factory.CreateClient();

        var response = await anonymous.PostAsync(
            new Uri(
                $"{HubPath}/negotiate?negotiateVersion=1&access_token={auth.Tokens.AccessToken}",
                UriKind.Relative),
            null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// <b>And nowhere else.</b> A token in a query string is a token in an access log, a proxy cache
    /// and a browser history; it is tolerated on the hub because the transport leaves no
    /// alternative, and accepting it on the REST surface would hand every caller a way to put a
    /// bearer token somewhere it gets written down.
    /// </summary>
    [Fact]
    public async Task A_query_string_token_does_not_authenticate_the_rest_surface()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        using var anonymous = Factory.CreateClient();

        var response = await anonymous.GetAsync(
            new Uri($"{Root}?access_token={auth.Tokens.AccessToken}", UriKind.Relative));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    // ── Connection lifecycle ────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The end-to-end proof.</b> A real client negotiates, authenticates, handshakes and is told
    /// who the server thinks it is — which is the assertion that catches a user-identifier provider
    /// reading the wrong claim, the failure mode where every fan-out silently reaches nobody.
    /// </summary>
    [Fact]
    public async Task An_authenticated_client_connects_and_is_acknowledged()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        var acknowledged = new TaskCompletionSource<RealtimeHandshakeShape>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        await using var connection = BuildConnection(auth.Tokens.AccessToken);

        connection.On<RealtimeHandshakeShape>(
            "ConnectionAcknowledged", handshake => acknowledged.TrySetResult(handshake));

        await connection.StartAsync();

        var received = await WithTimeoutAsync(acknowledged.Task);

        received.UserId.Should().Be(auth.User.Id);
        received.ConnectionId.Should().NotBeNullOrWhiteSpace();
        received.PresenceEnabled.Should().BeTrue();
    }

    /// <summary>
    /// The registry is what presence, the connection cap and every fan-out read. A connection that
    /// completed a handshake without being registered would be a member the platform believes is
    /// offline while they are looking at the app.
    /// </summary>
    [Fact]
    public async Task A_connected_member_is_visible_to_the_connection_registry()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        await using var connection = BuildConnection(auth.Tokens.AccessToken);
        await connection.StartAsync();

        var registry = Factory.Services.GetRequiredService<IRealtimeConnectionRegistry>();
        var userId = SharedKernel.Identity.UserId.From(auth.User.Id);

        // Polled rather than asserted once. StartAsync completes when the *client* handshake is
        // done, which is not the same instant the server finishes OnConnectedAsync — asserting
        // immediately is a race that passes on a fast machine and fails in CI.
        (await EventuallyAsync(() => registry.IsConnectedAsync(userId))).Should().BeTrue();

        await connection.StopAsync();

        (await EventuallyAsync(async () => !await registry.IsConnectedAsync(userId))).Should().BeTrue(
            "a socket that closed and left the member connected is how presence starts lying");
    }

    [Fact]
    public async Task The_caller_reads_their_own_presence_over_http()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        await using var connection = BuildConnection(auth.Tokens.AccessToken);
        await connection.StartAsync();

        var response = await HttpClient.GetAsync(
            new Uri($"{Root}/realtime/presence", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var presence = await ReadDataAsync<PresenceShape>(response);

        presence.Status.Should().Be("online");
        presence.ConnectionCount.Should().Be(1);
    }

    // ── Group authorisation ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_member_may_join_their_own_group()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        await using var connection = BuildConnection(auth.Tokens.AccessToken);
        await connection.StartAsync();

        var joined = await connection.InvokeAsync<bool>(
            "JoinGroup", $"user:{auth.User.Id:D}");

        joined.Should().BeTrue();
    }

    /// <summary>
    /// <b>The refusal that keeps the runtime honest about what it does not own.</b> A client can
    /// name any group. Nothing in this platform can say who is in a conversation, so a runtime that
    /// let one join would relay typing, read receipts and delivery acknowledgements to whoever
    /// guessed an identifier — silently, and to somebody who is not in the conversation.
    /// </summary>
    [Fact]
    public async Task A_member_may_not_join_a_conversation_group()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        await using var connection = BuildConnection(auth.Tokens.AccessToken);
        await connection.StartAsync();

        var joined = await connection.InvokeAsync<bool>(
            "JoinGroup", $"conversation:{Guid.NewGuid():D}");

        joined.Should().BeFalse();
    }

    [Fact]
    public async Task A_member_may_not_join_somebody_elses_group()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        var (_, other) = await RegisterAndVerifyAsync();

        await using var connection = BuildConnection(auth.Tokens.AccessToken);
        await connection.StartAsync();

        var joined = await connection.InvokeAsync<bool>(
            "JoinGroup", $"user:{other.User.Id:D}");

        joined.Should().BeFalse("that group carries the other member's notifications");
    }

    // ── Channel families, B13.3 ─────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Four families claimed, each by the module that owns the subject.</b>
    /// <para>
    /// The composition B13.3 replaced a single-winner seam with. Until then exactly one module could
    /// own real-time channels — the Conversations Platform replaced the shipped authorizer outright —
    /// and the second vertical to try would have silently switched conversations off. This asserts
    /// that all four are present together in a real host, which is the thing the old arrangement
    /// could not do.
    /// </para>
    /// </summary>
    [Fact]
    public void Every_channel_family_has_an_owner_in_a_composed_host()
    {
        using var scope = Factory.Services.CreateScope();

        var registry = scope.ServiceProvider.GetRequiredService<RealtimeChannelRegistry>();

        registry.OwnedPrefixes.Should().BeEquivalentTo(
            ["user:", "conversation:", "organization:", "event:"],
            "the Communication Platform owns the member's own channel, the Conversations Platform "
            + "owns conversations, and Community Life owns organizations and events — all at once, "
            + "which is the whole point of the prefix registry");
    }

    /// <summary>
    /// <b>The IDOR test for the two families B13.3 added.</b>
    /// <para>
    /// A client supplies the channel name, and an identifier is cheap to guess when the client is
    /// what tells you which ones exist. Community Life answers both of these against its own tables:
    /// an organization channel needs an active membership, an event channel needs a live RSVP or the
    /// organizer's own account. A member with neither is refused, and the refusal is an ordinary
    /// answer rather than an error frame.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_member_may_not_join_an_organization_or_event_they_have_no_part_in()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        await using var connection = BuildConnection(auth.Tokens.AccessToken);
        await connection.StartAsync();

        (await connection.InvokeAsync<bool>("JoinGroup", $"organization:{Guid.NewGuid():D}"))
            .Should().BeFalse("membership is what admits somebody to an organization's channel");

        (await connection.InvokeAsync<bool>("JoinGroup", $"event:{Guid.NewGuid():D}"))
            .Should().BeFalse("an RSVP is the subscription to an event's channel");
    }

    /// <summary>
    /// <b>A name beneath no claimed prefix is refused, and that is the shipped answer for everything
    /// a client can invent.</b> The hub accepts an arbitrary string from a socket; this is what
    /// stops the string from being able to do anything.
    /// </summary>
    [Theory]
    [InlineData("everybody")]
    [InlineData("admin:all")]
    [InlineData("community:00000000-0000-0000-0000-000000000001")]
    [InlineData("organization:not-a-guid")]
    [InlineData("event:")]
    public async Task A_channel_beneath_no_claimed_prefix_is_refused(string channel)
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        await using var connection = BuildConnection(auth.Tokens.AccessToken);
        await connection.StartAsync();

        (await connection.InvokeAsync<bool>("JoinGroup", channel)).Should().BeFalse();
    }

    /// <summary>
    /// Relays are authorised by the same seam a join is, so a client cannot skip the join and
    /// broadcast into a group it was never let into.
    /// </summary>
    [Fact]
    public async Task Typing_and_receipts_are_refused_for_an_unauthorised_conversation()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        var conversationId = Guid.NewGuid();

        await using var connection = BuildConnection(auth.Tokens.AccessToken);
        await connection.StartAsync();

        (await connection.InvokeAsync<bool>("Typing", conversationId, "TypingStarted"))
            .Should().BeFalse();
        (await connection.InvokeAsync<bool>("MarkRead", conversationId, Guid.NewGuid()))
            .Should().BeFalse();
        (await connection.InvokeAsync<bool>("AcknowledgeDelivery", conversationId, Guid.NewGuid()))
            .Should().BeFalse();
    }

    // ── Live delivery ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The whole runtime, end to end.</b> A member connects; the platform pushes to their own
    /// group; the payload arrives in the shape the client binds to. This is the path every
    /// notification takes to a connected client, and the one that would otherwise be discovered
    /// broken by a mobile developer rather than by this repository.
    /// </summary>
    [Fact]
    public async Task A_notification_pushed_to_a_member_reaches_their_connected_client()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        var received = new TaskCompletionSource<RealtimeNotificationShape>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var acknowledged = new TaskCompletionSource<RealtimeHandshakeShape>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        await using var connection = BuildConnection(auth.Tokens.AccessToken);

        connection.On<RealtimeNotificationShape>(
            "NotificationReceived", notification => received.TrySetResult(notification));

        connection.On<RealtimeHandshakeShape>(
            "ConnectionAcknowledged", handshake => acknowledged.TrySetResult(handshake));

        await connection.StartAsync();

        // Waiting for the acknowledgement rather than for StartAsync, and the ordering is the
        // reason the acknowledgement exists. StartAsync completes when the client handshake is
        // done; the server joins the member to their own group afterwards, inside OnConnectedAsync,
        // and only then sends the acknowledgement. Pushing before it arrives is a race that fans out
        // to an empty group — which is silent, and is exactly the failure this whole test exists to
        // catch, so the test must not be able to produce it by accident.
        await WithTimeoutAsync(acknowledged.Task);

        var notifier = Factory.Services.GetRequiredService<IRealtimeNotifier>();
        var notificationId = Guid.NewGuid();

        await notifier.SendToUserAsync(
            SharedKernel.Identity.UserId.From(auth.User.Id),
            RealtimeEvents.NotificationReceived,
            new RealtimeNotification(
                notificationId,
                "Your password was changed",
                "If this was not you, secure your account now.",
                "security",
                "Critical",
                DateTimeOffset.UtcNow,
                "/settings/security",
                null));

        var notification = await WithTimeoutAsync(received.Task);

        notification.Id.Should().Be(notificationId);
        notification.CategoryKey.Should().Be("security");
        notification.Priority.Should().Be("Critical");
        notification.Title.Should().Be("Your password was changed");
        notification.TargetRoute.Should().Be("/settings/security");
    }

    /// <summary>
    /// A member with no live socket is the normal case, not a failure — the notification is already
    /// in their inbox and push exists for exactly this. A notifier that threw would make every
    /// caller wrap it, and the first one that forgot would fail a delivery because somebody's phone
    /// was in a tunnel.
    /// </summary>
    [Fact]
    public async Task Pushing_to_a_member_with_no_connection_is_not_an_error()
    {
        var notifier = Factory.Services.GetRequiredService<IRealtimeNotifier>();

        var send = async () => await notifier.SendToUserAsync(
            SharedKernel.Identity.UserId.New(),
            RealtimeEvents.InboxCountersChanged,
            new InboxCounters(0, new Dictionary<string, int>(), DateTimeOffset.UtcNow));

        await send.Should().NotThrowAsync();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A client wired to the in-memory test server.
    /// <para>
    /// Long polling is forced because <c>TestServer</c> has no socket to upgrade. Everything under
    /// test above the transport — negotiate, authentication, the user-identifier provider, group
    /// membership, dispatch and JSON serialisation — is identical on either.
    /// </para>
    /// </summary>
    private HubConnection BuildConnection(string accessToken) =>
        new HubConnectionBuilder()
            .WithUrl(
                new Uri(Factory.Server.BaseAddress, HubPath.TrimStart('/')),
                options =>
                {
                    options.Transports = HttpTransportType.LongPolling;
                    options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
                    options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
                })
            .Build();

    /// <summary>
    /// Fails a waiting assertion rather than hanging the suite.
    /// <para>
    /// A real-time test that never receives its event would otherwise block until the whole run is
    /// killed, and a killed run reports nothing about which assertion was waiting.
    /// </para>
    /// </summary>
    private static async Task<T> WithTimeoutAsync<T>(Task<T> task)
    {
        var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10)));

        completed.Should().BeSameAs(task, "the expected real-time event never arrived");

        return await task;
    }

    /// <summary>
    /// Waits for a server-side fact to settle.
    /// <para>
    /// Connection bookkeeping happens on the server after the client's handshake completes, so a
    /// test that asserts the instant <c>StartAsync</c> returns is asserting against a race. Polling
    /// to a deadline is the honest shape: it fails in bounded time and it does not pass only on a
    /// fast machine.
    /// </para>
    /// </summary>
    private static async Task<bool> EventuallyAsync(Func<Task<bool>> condition)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (await condition())
            {
                return true;
            }

            await Task.Delay(100);
        }

        return false;
    }

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var envelope = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        return envelope.GetProperty("data").Deserialize<T>(JsonOptions)!;
    }

    // The wire shapes, re-declared from the client's side. Binding these to the module's own
    // records would make them pass whenever the module compiles, which is the one thing a contract
    // test must never do.

    private sealed record RealtimeConfigurationShape
    {
        public bool Enabled { get; init; }

        public string HubPath { get; init; } = string.Empty;

        public bool PresenceEnabled { get; init; }

        public bool TypingEnabled { get; init; }

        public int TypingTimeoutSeconds { get; init; }

        public bool ReadReceiptsEnabled { get; init; }

        public bool DeliveryReceiptsEnabled { get; init; }

        public bool ConversationRelayEnabled { get; init; }

        public int HeartbeatSeconds { get; init; }

        public int MaxConnectionsPerUser { get; init; }
    }

    private sealed record RealtimeHandshakeShape
    {
        public string ConnectionId { get; init; } = string.Empty;

        public Guid UserId { get; init; }

        public DateTimeOffset ServerTime { get; init; }

        public bool PresenceEnabled { get; init; }

        public bool TypingEnabled { get; init; }

        public bool ConversationRelayEnabled { get; init; }
    }

    // Spelled exactly as the REST inbox spells it, which is the point: a client renders a live
    // arrival and a fetched one through one code path, and this shape is what proves the two agree.
    private sealed record RealtimeNotificationShape
    {
        public Guid Id { get; init; }

        public string Title { get; init; } = string.Empty;

        public string Body { get; init; } = string.Empty;

        public string CategoryKey { get; init; } = string.Empty;

        public string Priority { get; init; } = string.Empty;

        public DateTimeOffset CreatedAt { get; init; }

        public string? TargetRoute { get; init; }
    }

    private sealed record PresenceShape
    {
        public string Status { get; init; } = string.Empty;

        public DateTimeOffset? LastSeenAt { get; init; }

        public int ConnectionCount { get; init; }
    }
}
