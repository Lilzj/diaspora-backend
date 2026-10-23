using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using Diaspora.Platform.Messaging.Outbox.Model;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Platform;

/// <summary>
/// B1.1 end to end, over real HTTP, through the host every other suite uses.
/// <para>
/// The unit suite proves the outbox's parts against a real database. This proves the thing that
/// actually matters to the platform: that a member's ordinary request, through an unmodified
/// module, now produces a durable message — and that the module in question was never told.
/// </para>
/// <para>
/// The background dispatcher is off in this host and delivery is driven explicitly, so no test
/// here asserts on a timer having fired.
/// </para>
/// </summary>
[Collection(PlatformCollection.Name)]
public sealed class ReliableMessagingTests : PooledIdentityTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ReliableMessagingTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── The producing side ───────────────────────────────────────────────

    /// <summary>
    /// <b>The phase in one test.</b> A member publishes a post through the Community module —
    /// a module that was not edited for B1.1 and does not reference the outbox — and the
    /// platform is left holding a durable, committed message describing it.
    /// </summary>
    [Fact]
    public async Task An_ordinary_request_leaves_a_durable_message_behind()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var before = (await Factory.PendingOutboxAsync()).Count;

        var response = await HttpClient.PostAsJsonAsync(
            "/api/v1/community/posts",
            new { body = "The outbox is watching.", visibility = "public", publish = true },
            JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var pending = await Factory.PendingOutboxAsync();

        pending.Count.Should().BeGreaterThan(before,
            "a committed post must leave a committed promise to announce it");

        pending.Should().OnlyContain(message => message.State == OutboxState.Pending);
        pending.Should().OnlyContain(message => message.Payload.Length > 0);
    }

    /// <summary>
    /// The message carries the producing request's correlation identifier, captured at enqueue
    /// rather than at dispatch — the dispatcher runs later on a thread with no request behind it,
    /// and anything it read from ambient context would belong to nobody.
    /// </summary>
    [Fact]
    public async Task A_message_carries_the_producing_requests_correlation()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        await HttpClient.PostAsJsonAsync(
            "/api/v1/community/posts",
            new { body = "Correlated.", visibility = "public", publish = true },
            JsonOptions);

        var pending = await Factory.PendingOutboxAsync();

        pending.Should().NotBeEmpty();
        pending.Should().Contain(message => message.CorrelationId != null);
    }

    /// <summary>
    /// The stored identity is a contract name, never a CLR type name. This is the property that
    /// keeps a message written today readable by the deployment that ships next week.
    /// </summary>
    [Fact]
    public async Task Stored_messages_name_a_contract_rather_than_an_assembly()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        await HttpClient.PostAsJsonAsync(
            "/api/v1/community/posts",
            new { body = "Named.", visibility = "public", publish = true },
            JsonOptions);

        var pending = await Factory.PendingOutboxAsync();

        pending.Should().NotBeEmpty();
        pending.Should().OnlyContain(message => !message.EventType.Contains("Version="));
        pending.Should().OnlyContain(message => !message.EventType.Contains("PublicKeyToken="));
        pending.Should().OnlyContain(message => message.EventType.StartsWith("Diaspora.Contracts."));
    }

    // ── The consuming side ───────────────────────────────────────────────

    /// <summary>
    /// <b>Consumer compatibility, demonstrated rather than asserted.</b> Every message the suite
    /// has produced is drained through the real in-process transport, into the same handlers
    /// every module registered before B1.1 — none of which was changed. A handler that could not
    /// cope would surface here as a dead letter.
    /// </summary>
    [Fact]
    public async Task Every_queued_message_is_delivered_to_the_existing_consumers()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        await HttpClient.PostAsJsonAsync(
            "/api/v1/community/posts",
            new { body = "Delivered.", visibility = "public", publish = true },
            JsonOptions);

        (await Factory.PendingOutboxAsync()).Should().NotBeEmpty();

        await Factory.DrainOutboxAsync();

        (await Factory.PendingOutboxAsync()).Should().BeEmpty(
            "every message reached a consumer that accepted it");

        (await Factory.DeadLettersAsync()).Should().BeEmpty(
            "no existing handler rejected a message it used to receive in-process");
    }

    /// <summary>
    /// Draining is idempotent from the queue's side: a delivered message is not delivered again
    /// by the next pass. At-least-once is the contract, not an excuse for at-least-twice.
    /// </summary>
    [Fact]
    public async Task A_delivered_message_is_not_claimed_again()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        await HttpClient.PostAsJsonAsync(
            "/api/v1/community/posts",
            new { body = "Once.", visibility = "public", publish = true },
            JsonOptions);

        await Factory.DrainOutboxAsync();

        var second = await Factory.DispatchOnceAsync();

        second.Claimed.Should().Be(0);
    }

    // ── Observability ────────────────────────────────────────────────────

    /// <summary>
    /// Readiness reports the queue. An instance whose outbox has stopped moving should stop
    /// taking write traffic that adds to a queue nothing is draining.
    /// <para>
    /// The assertion is on the outbox entry rather than on the overall status, deliberately.
    /// Readiness aggregates every check, and this suite's host shares one in-memory SQLite
    /// connection across every scope — an arrangement that occasionally makes a check report a
    /// connection-level artefact that has nothing to do with what it checks. Asserting on the
    /// aggregate would make this test fail for a reason it does not cover, which is how a suite
    /// stops being read.
    /// </para>
    /// <para>
    /// The outbox check is not immune either: it queries the same connection, concurrently with
    /// the others, and under a full run it occasionally threw — reported as <c>Unhealthy</c> with
    /// no data. <see cref="ReadinessProbe"/> reads past a check that threw and never past one that
    /// evaluated, so a genuinely unhealthy outbox still fails here on the first read.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Readiness_reports_the_outbox()
    {
        await Factory.DrainOutboxAsync();

        var outbox = await HttpClient.ReadinessCheckAsync("outbox");

        outbox.GetProperty("status").GetString().Should().Be("Healthy", outbox.GetRawText());

        // The numbers an operator needs, on the endpoint every probe already calls.
        var data = outbox.GetProperty("data");

        data.GetProperty("pending").GetInt32().Should().Be(0);
        data.GetProperty("deadLettered").GetInt32().Should().Be(0);
        data.GetProperty("transport").GetString().Should().Be("in-process");
        data.TryGetProperty("oldestPendingSeconds", out _).Should().BeTrue();
        data.TryGetProperty("retrying", out _).Should().BeTrue();
    }

    /// <summary>
    /// The dispatcher is off in this host, so the health check has to read for itself rather
    /// than trusting a snapshot nothing refreshes — the same situation as an API pod in a
    /// deployment where only worker pods dispatch.
    /// </summary>
    [Fact]
    public async Task An_instance_that_does_not_dispatch_still_reports_the_queue()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        await HttpClient.PostAsJsonAsync(
            "/api/v1/community/posts",
            new { body = "Observed.", visibility = "public", publish = true },
            JsonOptions);

        var outbox = await HttpClient.ReadinessCheckAsync("outbox");

        outbox.GetProperty("data").TryGetProperty("pending", out _).Should().BeTrue(outbox.GetRawText());
    }

    // ── Administration ───────────────────────────────────────────────────

    /// <summary>
    /// The operator surface is closed to members. Replaying or skipping a message changes what
    /// other modules believe happened, and no amount of progression through the access tiers
    /// reaches it.
    /// </summary>
    [Fact]
    public async Task A_verified_member_cannot_read_the_outbox()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync("/api/v1/platform/outbox/statistics");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_anonymous_caller_cannot_read_the_outbox()
    {
        using var anonymous = Factory.CreateClient();

        var response = await anonymous.GetAsync("/api/v1/platform/outbox/statistics");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_verified_member_cannot_skip_a_message()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.PostAsJsonAsync(
            "/api/v1/platform/outbox/pending/1/skip",
            new { reason = "trying it on" },
            JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
