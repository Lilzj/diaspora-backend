using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Integrations.Domain.Connections;
using Diaspora.Modules.Integrations.Domain.Enums;
using Diaspora.Modules.Integrations;
using Diaspora.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// B22 — the integration foundation, driven over real HTTP against a deterministic provider.
/// <para>
/// <b>Every assertion here exercises both sides of the thing it asserts.</b> Wave 4's most expensive
/// finding restated for this surface: a suite that only ever proved a refusal would pass against a
/// module that could do nothing at all. So these tests connect, write, disconnect, replay, collide
/// and isolate — and assert on what happens as well as on what is refused.
/// </para>
/// <para>
/// <b>What is verified and what is not.</b> Everything below is this platform's half: the state and
/// PKCE gates, the single-use refusal, the account-conflict 409, custody, the duplicate barrier, the
/// read-only refusal, the signature gate, isolation and the disconnect's cascade. Google's own API
/// surface is not exercised anywhere and is classified as an external dependency in the wave report
/// — §48 forbids claiming a real provider integration is verified when it was never executed.
/// </para>
/// </summary>
public sealed class IntegrationFoundationTests
    : IdentityTestBase<IntegrationsWebApplicationFactory>
{
    private const string Root = "/api/v1/integrations";
    private const string Calendar = IntegrationsWebApplicationFactory.CalendarProvider;
    private const string Redirect = "https://app.diaspora.test/oauth/callback";

    /// <summary>A PKCE verifier of legal length (RFC 7636: 43–128 characters).</summary>
    private const string Verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk-integration";

    public IntegrationFoundationTests(IntegrationsWebApplicationFactory factory) : base(factory)
    {
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // FRONTEND-PHASE16-GAP-002 — the exchange. The register calls it "the blocker".
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The whole path: authorize, exchange, and a connection that reads back with no token on it.
    /// </summary>
    [Fact]
    public async Task A_member_connects_an_account_and_the_connection_carries_no_token()
    {
        var member = await ConnectedMemberAsync();

        var connections = await ReadAsync<JsonElement>(
            await HttpClient.GetAsync($"{Root}/connections"));

        var connection = connections.EnumerateArray()
            .Single(row => row.GetProperty("provider").GetString() == Calendar);

        connection.GetProperty("status").GetString().Should().Be("CONNECTED");
        connection.GetProperty("mode").GetString().Should().Be("BACKEND_CONNECTED");
        connection.GetProperty("account").GetProperty("accountLabel").GetString()
            .Should().Be($"{member.ExternalAccountId}@example.test");

        connection.GetProperty("scopes").EnumerateArray().Select(scope => scope.GetString())
            .Should().Contain("calendar.read");

        // The enforcement that matters. Nothing on the wire may be a credential.
        var raw = connection.GetRawText();
        raw.Should().NotContain("accessToken").And.NotContain("refreshToken")
            .And.NotContain("access_token").And.NotContain("refresh_token");
    }

    /// <summary>
    /// <b>A state this platform never issued matches nothing.</b>
    /// <para>
    /// "Never trust <c>state</c> without validation." The exchange is refused before a provider call
    /// is spent on it.
    /// </para>
    /// </summary>
    [Fact]
    public async Task An_exchange_with_an_unknown_state_is_refused()
    {
        await AuthenticateVerifiedMemberAsync();

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/oauth/{Calendar}/exchange",
            new
            {
                code = Factory.Provider.IssueCode("account-unknown-state"),
                codeVerifier = Verifier,
                state = "a-state-nobody-issued",
                redirectUri = Redirect,
                intent = "link"
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertErrorCodeAsync(response, "integrations.oauth.state-unknown");
    }

    /// <summary>The PKCE verifier must hash to the recorded challenge.</summary>
    [Fact]
    public async Task An_exchange_with_the_wrong_pkce_verifier_is_refused()
    {
        await AuthenticateVerifiedMemberAsync();

        var state = await StartAuthorizationAsync(Calendar);

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/oauth/{Calendar}/exchange",
            new
            {
                code = Factory.Provider.IssueCode("account-wrong-verifier"),
                codeVerifier = "a-completely-different-verifier-that-is-long-enough",
                state,
                redirectUri = Redirect,
                intent = "link"
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// A replayed authorization answers 409, and produces no second connection.
    /// <para>
    /// The register: "A replayed code MUST return 409, not a second connection." The status is what
    /// tells a client the first attempt worked.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_replayed_authorization_answers_conflict_and_creates_nothing()
    {
        await AuthenticateVerifiedMemberAsync();

        var state = await StartAuthorizationAsync(Calendar);
        var code = Factory.Provider.IssueCode($"account-replay-{Guid.NewGuid():N}");

        var body = new
        {
            code,
            codeVerifier = Verifier,
            state,
            redirectUri = Redirect,
            intent = "link"
        };

        (await HttpClient.PostAsJsonAsync($"{Root}/oauth/{Calendar}/exchange", body))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var replay = await HttpClient.PostAsJsonAsync($"{Root}/oauth/{Calendar}/exchange", body);

        replay.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await AssertErrorCodeAsync(replay, "integrations.oauth.code-replayed");

        var connections = await ReadAsync<JsonElement>(
            await HttpClient.GetAsync($"{Root}/connections"));

        connections.EnumerateArray()
            .Count(row => row.GetProperty("provider").GetString() == Calendar)
            .Should().Be(1, "a replay must never produce a second grant");
    }

    /// <summary>
    /// A redirect that is not on the allow-list is refused.
    /// <para>
    /// "Accepting a client-supplied redirect is the canonical OAuth code-interception
    /// vulnerability."
    /// </para>
    /// </summary>
    [Fact]
    public async Task An_authorization_to_an_unlisted_redirect_is_refused()
    {
        await AuthenticateVerifiedMemberAsync();

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/oauth/{Calendar}/authorize",
            new
            {
                redirectUri = "https://app.diaspora.test.attacker.example/oauth/callback",
                codeChallenge = Challenge(Verifier),
                scopes = new[] { "calendar.read" },
                intent = "link"
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertErrorCodeAsync(response, "integrations.oauth.redirect-not-allowed");
    }

    /// <summary>PKCE is mandatory: an authorization without a challenge does not start.</summary>
    [Fact]
    public async Task An_authorization_without_a_pkce_challenge_is_refused()
    {
        await AuthenticateVerifiedMemberAsync();

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/oauth/{Calendar}/authorize",
            new { redirectUri = Redirect, scopes = new[] { "calendar.read" }, intent = "link" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// No external identity can be linked, because no <c>identity.*</c> key parses.
    /// <para>
    /// Email and password is the only way to sign in (decided 5 Oct 2026). The OAuth routes still
    /// serve calendars; an identity provider is refused as unknown before any adapter is asked.
    /// </para>
    /// </summary>
    [Fact]
    public async Task An_identity_provider_cannot_be_authorized()
    {
        await AuthenticateVerifiedMemberAsync();

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/oauth/identity.google/authorize",
            new
            {
                redirectUri = Redirect,
                codeChallenge = Challenge(Verifier),
                scopes = new[] { "profile" },
                intent = "link"
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertErrorCodeAsync(response, "integrations.provider.unknown");
    }

    /// <summary>
    /// The scopes granted are the adapter's narrowing, not the client's request.
    /// <para>
    /// A client that adds a string to the array must not widen the grant.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_client_cannot_widen_a_grant_by_asking_for_more_scopes()
    {
        await AuthenticateVerifiedMemberAsync();

        var start = await ReadAsync<JsonElement>(await HttpClient.PostAsJsonAsync(
            $"{Root}/oauth/{Calendar}/authorize",
            new
            {
                redirectUri = Redirect,
                codeChallenge = Challenge(Verifier),
                scopes = new[] { "calendar.read", "admin.everything", "contacts.read" },
                intent = "link"
            }));

        start.GetProperty("scopes").EnumerateArray().Select(scope => scope.GetString())
            .Should().BeEquivalentTo(["calendar.read"]);
    }

    /// <summary>
    /// <b>An external account already attached to another member answers 409.</b>
    /// <para>
    /// "The 409 is a security control, not a niceness — without it, linking is an account merge
    /// anybody can trigger."
    /// </para>
    /// </summary>
    [Fact]
    public async Task Linking_an_account_that_belongs_to_another_member_answers_conflict()
    {
        var shared = $"shared-account-{Guid.NewGuid():N}";

        await ConnectedMemberAsync(externalAccountId: shared);

        // A second, entirely separate member attempts the same external account.
        await AuthenticateVerifiedMemberAsync();

        var state = await StartAuthorizationAsync(Calendar);

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/oauth/{Calendar}/exchange",
            new
            {
                code = Factory.Provider.IssueCode(shared),
                codeVerifier = Verifier,
                state,
                redirectUri = Redirect,
                intent = "link"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await AssertErrorCodeAsync(response, "integrations.identity.already-linked");
    }

    /// <summary>
    /// <b>A refused link leaves the authorization burnt, so it cannot be retried against somebody
    /// else.</b>
    /// <para>
    /// This one depends on pipeline behaviour rather than on the handler: the transaction behaviour
    /// rolls back on an <em>exception</em>, and a failed <c>Result</c> commits. If that ever
    /// changed, the consumed request would roll back with the refusal and the same state — with a
    /// code the provider has already spent — could be presented again. The second attempt answering
    /// <c>code-replayed</c> rather than <c>already-linked</c> is what proves the consumption
    /// committed.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_refused_link_still_consumes_the_authorization()
    {
        var shared = $"shared-burnt-{Guid.NewGuid():N}";

        await ConnectedMemberAsync(externalAccountId: shared);
        await AuthenticateVerifiedMemberAsync();

        var state = await StartAuthorizationAsync(Calendar);

        var body = new
        {
            code = Factory.Provider.IssueCode(shared),
            codeVerifier = Verifier,
            state,
            redirectUri = Redirect,
            intent = "link"
        };

        var refused = await HttpClient.PostAsJsonAsync($"{Root}/oauth/{Calendar}/exchange", body);
        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await AssertErrorCodeAsync(refused, "integrations.identity.already-linked");

        var retried = await HttpClient.PostAsJsonAsync($"{Root}/oauth/{Calendar}/exchange", body);
        retried.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await AssertErrorCodeAsync(retried, "integrations.oauth.code-replayed");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Isolation. §11 and §44.
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>One member's connection is unreachable by another under any identifier they can supply.</b>
    /// <para>
    /// There is no identifier to supply, which is the structural half: every route takes its subject
    /// from the token. This asserts the other half — that the data actually separates.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_member_never_sees_another_members_connection()
    {
        await ConnectedMemberAsync();

        var first = await ReadAsync<JsonElement>(await HttpClient.GetAsync($"{Root}/connections"));
        first.EnumerateArray().Should().NotBeEmpty();

        await AuthenticateVerifiedMemberAsync();

        var second = await ReadAsync<JsonElement>(await HttpClient.GetAsync($"{Root}/connections"));
        second.EnumerateArray().Should().BeEmpty(
            "a second member's connection list must be their own and empty");
    }

    /// <summary>
    /// A second member cannot reach the first member's calendar through the shared connection.
    /// </summary>
    [Fact]
    public async Task A_member_cannot_read_another_members_calendars()
    {
        await ConnectedMemberAsync();

        (await HttpClient.GetAsync($"{Root}/calendar/{Calendar}/calendars"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        await AuthenticateVerifiedMemberAsync();

        var response = await HttpClient.GetAsync($"{Root}/calendar/{Calendar}/calendars");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await AssertErrorCodeAsync(response, "integrations.connection.not-found");
    }

    /// <summary>An anonymous caller reaches nothing but the webhook route.</summary>
    [Fact]
    public async Task An_anonymous_caller_cannot_read_connections()
    {
        using var anonymous = Factory.CreateClient();

        (await anonymous.GetAsync($"{Root}/connections"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // FRONTEND-PHASE16-GAP-003 and GAP-004 — the calendar proxy.
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Calendars come back with the provider's real access role on each.</summary>
    [Fact]
    public async Task Calendars_report_the_providers_real_write_permission()
    {
        await ConnectedMemberAsync();

        var calendars = await ReadAsync<JsonElement>(
            await HttpClient.GetAsync($"{Root}/calendar/{Calendar}/calendars"));

        var primary = calendars.EnumerateArray()
            .Single(row => row.GetProperty("id").GetString() == "primary");

        primary.GetProperty("allowsModifications").GetBoolean().Should().BeTrue();

        var shared = calendars.EnumerateArray()
            .Single(row => row.GetProperty("id").GetString() == "shared-readonly");

        shared.GetProperty("allowsModifications").GetBoolean().Should().BeFalse(
            "assuming true produces a write that fails at the provider after the UI offered a button");
    }

    /// <summary>
    /// <b>A retried write with the same idempotency key produces one calendar entry.</b>
    /// <para>
    /// The register: a duplicate is "the most visible possible failure of this integration, and one
    /// a member cannot easily undo". The assertion is on the provider's call count, not on the
    /// response — a handler that called twice and returned the first answer would pass a weaker
    /// test.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_retried_calendar_write_reaches_the_provider_once()
    {
        await ConnectedMemberAsync();

        Factory.Provider.CreateCalls = 0;

        var key = $"attempt-{Guid.NewGuid():N}";
        var draft = CalendarDraft($"event-{Guid.NewGuid():N}");

        var first = await PostCalendarEventAsync(draft, key);
        var second = await PostCalendarEventAsync(draft, key);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);

        Factory.Provider.CreateCalls.Should().Be(1, "the second call is the same attempt replayed");

        var firstBody = await ReadAsync<JsonElement>(first);
        var secondBody = await ReadAsync<JsonElement>(second);

        secondBody.GetProperty("externalEventId").GetString()
            .Should().Be(firstBody.GetProperty("externalEventId").GetString());

        secondBody.GetProperty("wasDeduplicated").GetBoolean().Should().BeTrue();
        firstBody.GetProperty("wasDeduplicated").GetBoolean().Should().BeFalse();
    }

    /// <summary>
    /// An unchanged event re-exported under a new key still reaches the provider once.
    /// <para>
    /// The second barrier: the content fingerprint. This is what makes a nightly re-export cheap
    /// instead of a rewrite of every entry.
    /// </para>
    /// </summary>
    [Fact]
    public async Task An_unchanged_event_re_exported_does_not_reach_the_provider_again()
    {
        await ConnectedMemberAsync();

        Factory.Provider.CreateCalls = 0;

        var draft = CalendarDraft($"event-{Guid.NewGuid():N}");

        await PostCalendarEventAsync(draft, $"attempt-{Guid.NewGuid():N}");
        var again = await PostCalendarEventAsync(draft, $"attempt-{Guid.NewGuid():N}");

        Factory.Provider.CreateCalls.Should().Be(1);
        (await ReadAsync<JsonElement>(again)).GetProperty("wasDeduplicated").GetBoolean()
            .Should().BeTrue();
    }

    /// <summary>
    /// A changed event updates the entry the provider already holds rather than writing a second.
    /// </summary>
    [Fact]
    public async Task A_changed_event_updates_the_existing_entry()
    {
        await ConnectedMemberAsync();

        Factory.Provider.CreateCalls = 0;

        var sourceId = $"event-{Guid.NewGuid():N}";

        var created = await ReadAsync<JsonElement>(
            await PostCalendarEventAsync(CalendarDraft(sourceId), $"attempt-{Guid.NewGuid():N}"));

        var updated = await ReadAsync<JsonElement>(
            await PostCalendarEventAsync(
                CalendarDraft(sourceId, title: "Community meeting — moved"),
                $"attempt-{Guid.NewGuid():N}"));

        Factory.Provider.CreateCalls.Should().Be(1, "the second write is an update, not a create");

        updated.GetProperty("externalEventId").GetString()
            .Should().Be(created.GetProperty("externalEventId").GetString());

        updated.GetProperty("wasDeduplicated").GetBoolean().Should().BeFalse();
    }

    /// <summary>A write with no idempotency key is refused, because a retry would duplicate.</summary>
    [Fact]
    public async Task A_calendar_write_without_an_idempotency_key_is_refused()
    {
        await ConnectedMemberAsync();

        var response = await PostCalendarEventAsync(
            CalendarDraft($"event-{Guid.NewGuid():N}"), idempotencyKey: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// A write with no time zone is refused rather than defaulted to the server's.
    /// <para>
    /// "A calendar entry that says 17:00 BST is a different — and wrong — claim about a real-world
    /// event."
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_calendar_write_without_a_time_zone_is_refused()
    {
        await ConnectedMemberAsync();

        var response = await PostCalendarEventAsync(
            CalendarDraft($"event-{Guid.NewGuid():N}", timeZone: null),
            $"attempt-{Guid.NewGuid():N}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertErrorCodeAsync(response, "integrations.calendar.timezone-required");
    }

    /// <summary>An unknown zone is refused rather than interpreted as UTC by the provider.</summary>
    [Fact]
    public async Task A_calendar_write_with_an_unknown_time_zone_is_refused()
    {
        await ConnectedMemberAsync();

        var response = await PostCalendarEventAsync(
            CalendarDraft($"event-{Guid.NewGuid():N}", timeZone: "Mars/Olympus_Mons"),
            $"attempt-{Guid.NewGuid():N}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertErrorCodeAsync(response, "integrations.calendar.timezone-unknown");
    }

    /// <summary>
    /// A read-only calendar answers 403, never a silent no-op.
    /// <para>
    /// §46: "a calendar id is a NAME, not a capability — the server re-derives access from the
    /// connection."
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_write_to_a_read_only_calendar_is_forbidden()
    {
        await ConnectedMemberAsync();

        var response = await PostCalendarEventAsync(
            CalendarDraft($"event-{Guid.NewGuid():N}", calendarId: "shared-readonly"),
            $"attempt-{Guid.NewGuid():N}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await AssertErrorCodeAsync(response, "integrations.calendar.read-only");
    }

    /// <summary>A calendar the grant cannot see answers 404 rather than being written to.</summary>
    [Fact]
    public async Task A_write_to_a_calendar_the_grant_cannot_see_is_refused()
    {
        await ConnectedMemberAsync();

        var response = await PostCalendarEventAsync(
            CalendarDraft($"event-{Guid.NewGuid():N}", calendarId: "somebody-elses-calendar"),
            $"attempt-{Guid.NewGuid():N}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>The lookup answers from the platform's own mapping, before and after a write.</summary>
    [Fact]
    public async Task The_lookup_reports_whether_an_entry_was_exported()
    {
        await ConnectedMemberAsync();

        var sourceId = $"event-{Guid.NewGuid():N}";
        var lookup = $"{Root}/calendar/{Calendar}/events/lookup?sourceId={sourceId}&calendarId=primary";

        var before = await HttpClient.GetAsync(lookup);
        before.StatusCode.Should().Be(HttpStatusCode.OK);
        (await before.Content.ReadAsStringAsync()).Should().Contain("\"data\":null");

        await PostCalendarEventAsync(CalendarDraft(sourceId), $"attempt-{Guid.NewGuid():N}");

        var after = await ReadAsync<JsonElement>(await HttpClient.GetAsync(lookup));
        after.GetProperty("externalEventId").GetString().Should().NotBeNullOrEmpty();
    }

    /// <summary>Deleting is idempotent: a second delete is a 200, not a 404.</summary>
    [Fact]
    public async Task Deleting_a_calendar_entry_twice_is_not_an_error()
    {
        await ConnectedMemberAsync();

        var written = await ReadAsync<JsonElement>(
            await PostCalendarEventAsync(
                CalendarDraft($"event-{Guid.NewGuid():N}"), $"attempt-{Guid.NewGuid():N}"));

        var externalId = written.GetProperty("externalEventId").GetString();
        var route = $"{Root}/calendar/{Calendar}/events/{externalId}";

        (await HttpClient.DeleteAsync(route)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await HttpClient.DeleteAsync(route)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// An entry this platform did not write cannot be deleted through it.
    /// <para>
    /// A client that could pass any external id would be a client that could delete arbitrary
    /// entries from a member's calendar using a grant given for export.
    /// </para>
    /// </summary>
    [Fact]
    public async Task An_entry_this_platform_never_wrote_cannot_be_deleted_through_it()
    {
        await ConnectedMemberAsync();

        Factory.Provider.Events["not-ours"] =
            new Diaspora.Modules.Integrations.Extensibility.Providers.ExternalCalendarEvent(
                "not-ours", "primary", DateTimeOffset.UtcNow);

        (await HttpClient.DeleteAsync($"{Root}/calendar/{Calendar}/events/not-ours"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        Factory.Provider.Events.Should().ContainKey("not-ours",
            "the entry belongs to the member and was not written by this platform");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // FRONTEND-PHASE16-GAP-005 — synchronisation.
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>A member who has never synced reads `never`, not a zeroed success.</summary>
    [Fact]
    public async Task Sync_status_before_any_run_reports_never()
    {
        await ConnectedMemberAsync();

        var status = await ReadAsync<JsonElement>(
            await HttpClient.GetAsync($"{Root}/calendar/{Calendar}/sync/status"));

        status.GetProperty("phase").GetString().Should().Be("never");
        status.GetProperty("lastSyncedAt").ValueKind.Should().Be(JsonValueKind.Null);
    }

    /// <summary>An export sync confirms what this platform wrote and reports a real count.</summary>
    [Fact]
    public async Task An_export_sync_confirms_what_was_written()
    {
        await ConnectedMemberAsync();

        await PostCalendarEventAsync(
            CalendarDraft($"event-{Guid.NewGuid():N}"), $"attempt-{Guid.NewGuid():N}");

        var run = await ReadAsync<JsonElement>(await HttpClient.PostAsJsonAsync(
            $"{Root}/calendar/{Calendar}/sync",
            new { calendarId = "primary", direction = "export" }));

        run.GetProperty("phase").GetString().Should().Be("synced");
        run.GetProperty("itemsSynced").GetInt32().Should().BeGreaterThan(0);
        run.GetProperty("lastSyncedAt").ValueKind.Should().NotBe(JsonValueKind.Null);
    }

    /// <summary>
    /// A sync notices an entry the member deleted at the provider, and does not put it back.
    /// </summary>
    [Fact]
    public async Task A_sync_notices_an_entry_the_member_deleted_and_does_not_restore_it()
    {
        await ConnectedMemberAsync();

        var written = await ReadAsync<JsonElement>(
            await PostCalendarEventAsync(
                CalendarDraft($"event-{Guid.NewGuid():N}"), $"attempt-{Guid.NewGuid():N}"));

        var externalId = written.GetProperty("externalEventId").GetString()!;

        // The member deletes it in their own calendar, which is a legitimate act.
        Factory.Provider.Events.TryRemove(externalId, out _);
        Factory.Provider.CreateCalls = 0;

        await HttpClient.PostAsJsonAsync(
            $"{Root}/calendar/{Calendar}/sync",
            new { calendarId = "primary", direction = "export" });

        Factory.Provider.CreateCalls.Should().Be(0,
            "putting a member's deleted entry back would be the integration arguing with them");

        Factory.Provider.Events.Should().NotContainKey(externalId);
    }

    /// <summary>
    /// Import is refused rather than silently performed as an export.
    /// <para>
    /// A member who asked for "both" and received an export would believe their calendar had been
    /// read into Diaspora, which is a consent claim rather than a feature claim.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("import")]
    [InlineData("both")]
    public async Task An_unsupported_sync_direction_is_refused(string direction)
    {
        await ConnectedMemberAsync();

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/calendar/{Calendar}/sync", new { calendarId = "primary", direction });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertErrorCodeAsync(response, "integrations.calendar.sync.direction-unsupported");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // FRONTEND-PHASE16-GAP-006 — settings.
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Settings default without being written, and a partial update leaves the rest alone.</summary>
    [Fact]
    public async Task A_partial_settings_update_leaves_every_other_field_alone()
    {
        await AuthenticateVerifiedMemberAsync();

        var defaults = await ReadAsync<JsonElement>(
            await HttpClient.GetAsync($"{Root}/calendar/settings"));

        defaults.GetProperty("enabled").GetBoolean().Should().BeFalse();
        defaults.GetProperty("timeZoneBehaviour").GetString().Should().Be("eventTimeZone");

        await HttpClient.PutAsJsonAsync(
            $"{Root}/calendar/settings",
            new { defaultCalendarId = "primary", defaultReminderMinutes = new[] { 30, 10 } });

        // Only `enabled` this time. The calendar must survive.
        var updated = await ReadAsync<JsonElement>(
            await HttpClient.PutAsJsonAsync($"{Root}/calendar/settings", new { enabled = true }));

        updated.GetProperty("enabled").GetBoolean().Should().BeTrue();
        updated.GetProperty("defaultCalendarId").GetString().Should().Be("primary");
        updated.GetProperty("defaultReminderMinutes").EnumerateArray()
            .Select(minutes => minutes.GetInt32()).Should().Equal(10, 30);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // FRONTEND-PHASE16-GAP-001 and GAP-007 — disconnect and identity.
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Disconnecting twice answers 200 both times.
    /// <para>
    /// "A 404 on the second call is exactly how a retry leaves [the UI] saying Connected."
    /// </para>
    /// </summary>
    [Fact]
    public async Task Disconnecting_twice_answers_the_same_thing()
    {
        await ConnectedMemberAsync();

        var first = await HttpClient.DeleteAsync($"{Root}/connections/{Calendar}");
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<JsonElement>(first)).GetProperty("revokedAtProvider").GetBoolean()
            .Should().BeTrue();

        var second = await HttpClient.DeleteAsync($"{Root}/connections/{Calendar}");
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<JsonElement>(second)).GetProperty("status").GetString()
            .Should().Be("DISCONNECTED");
    }

    /// <summary>Disconnecting a provider that was never connected is a 200, not a 404.</summary>
    [Fact]
    public async Task Disconnecting_something_never_connected_is_not_an_error()
    {
        await AuthenticateVerifiedMemberAsync();

        (await HttpClient.DeleteAsync($"{Root}/connections/{Calendar}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// A provider that cannot be reached does not block the disconnect — and the member is told.
    /// <para>
    /// Refusing to disconnect until the provider answers leaves the platform holding a refresh token
    /// it was told to forget.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_provider_outage_does_not_block_a_disconnect_and_is_reported()
    {
        await ConnectedMemberAsync();

        Factory.Provider.RevocationSucceeds = false;

        try
        {
            var response = await HttpClient.DeleteAsync($"{Root}/connections/{Calendar}");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await ReadAsync<JsonElement>(response)).GetProperty("revokedAtProvider").GetBoolean()
                .Should().BeFalse("the member is entitled to know the provider still holds it");
        }
        finally
        {
            Factory.Provider.RevocationSucceeds = true;
        }

        var connections = await ReadAsync<JsonElement>(
            await HttpClient.GetAsync($"{Root}/connections"));

        connections.EnumerateArray()
            .Single(row => row.GetProperty("provider").GetString() == Calendar)
            .GetProperty("status").GetString().Should().Be("DISCONNECTED");
    }

    /// <summary>
    /// A disconnect deletes the calendar links it stranded and forgets the default calendar.
    /// <para>
    /// §43's "what local integration data must be removed" has an unambiguous answer for both. The
    /// entries themselves stay in the member's calendar — deleting them would be destroying their
    /// data on an inference.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_disconnect_clears_the_state_the_dead_grant_stranded()
    {
        var member = await ConnectedMemberAsync();

        await HttpClient.PutAsJsonAsync(
            $"{Root}/calendar/settings",
            new { defaultProvider = Calendar, defaultCalendarId = "primary" });

        var written = await ReadAsync<JsonElement>(
            await PostCalendarEventAsync(
                CalendarDraft($"event-{Guid.NewGuid():N}"), $"attempt-{Guid.NewGuid():N}"));

        var externalId = written.GetProperty("externalEventId").GetString()!;

        await HttpClient.DeleteAsync($"{Root}/connections/{Calendar}");

        var settings = await ReadAsync<JsonElement>(
            await HttpClient.GetAsync($"{Root}/calendar/settings"));

        settings.GetProperty("defaultProvider").ValueKind.Should().Be(JsonValueKind.Null);
        settings.GetProperty("defaultCalendarId").ValueKind.Should().Be(JsonValueKind.Null);

        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        (await dbContext.Set<Diaspora.Modules.Integrations.Domain.Calendar.CalendarEventLink>()
                .CountAsync(link => link.OwnerId == member.UserId))
            .Should().Be(0, "a link exists only to deduplicate against a grant that is now gone");

        Factory.Provider.Events.Should().ContainKey(externalId,
            "the entry is the member's and stays in their calendar");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Webhooks. §14, §15, §16.
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>A revocation signal moves the connection to REAUTH_REQUIRED.</b>
    /// <para>
    /// This is what the register asks the webhook infrastructure to exist for: it stops the
    /// connection centre "showing <c>CONNECTED</c> for a grant removed at Google a week ago".
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_verified_revocation_callback_ends_the_grant()
    {
        var member = await ConnectedMemberAsync();

        var body = Payload("grant.revoked", member.ExternalAccountId);

        var response = await PostWebhookAsync(body, IntegrationsWebApplicationFactory.SignWebhook(body));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<JsonElement>(response)).GetProperty("outcome").GetString()
            .Should().Be("Applied");

        await AuthenticateAsync(member.AccessToken);

        var connections = await ReadAsync<JsonElement>(
            await HttpClient.GetAsync($"{Root}/connections"));

        connections.EnumerateArray()
            .Single(row => row.GetProperty("provider").GetString() == Calendar)
            .GetProperty("status").GetString().Should().Be("REAUTH_REQUIRED");
    }

    /// <summary>An invalid signature is refused, loudly, and writes no row.</summary>
    [Fact]
    public async Task A_callback_with_an_invalid_signature_is_refused()
    {
        var member = await ConnectedMemberAsync();
        var body = Payload("grant.revoked", member.ExternalAccountId);

        var response = await PostWebhookAsync(body, "0000000000000000");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertErrorCodeAsync(response, "integrations.webhook.signature-invalid");

        await AuthenticateAsync(member.AccessToken);

        var connections = await ReadAsync<JsonElement>(
            await HttpClient.GetAsync($"{Root}/connections"));

        connections.EnumerateArray()
            .Single(row => row.GetProperty("provider").GetString() == Calendar)
            .GetProperty("status").GetString().Should().Be("CONNECTED",
                "an unverified callback must change nothing");
    }

    /// <summary>A missing signature is refused for the same reason.</summary>
    [Fact]
    public async Task A_callback_with_no_signature_is_refused()
    {
        var body = Payload("grant.revoked", "whoever");

        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"{Root}/webhooks/{Calendar}")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        using var anonymous = Factory.CreateClient();

        using var response = await anonymous.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// A modified payload no longer verifies, because the signature covers the exact bytes.
    /// </summary>
    [Fact]
    public async Task A_modified_payload_no_longer_verifies()
    {
        var original = Payload("grant.revoked", "account-a");
        var signature = IntegrationsWebApplicationFactory.SignWebhook(original);
        var tampered = Payload("grant.revoked", "account-b");

        (await PostWebhookAsync(tampered, signature)).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// A redelivered callback is recognised and has no second effect.
    /// <para>
    /// Providers redeliver for days. The barrier is a unique index rather than a handler check.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_redelivered_callback_has_no_second_effect()
    {
        var member = await ConnectedMemberAsync();

        var eventId = $"provider-event-{Guid.NewGuid():N}";
        var body = Payload("grant.revoked", member.ExternalAccountId, eventId);
        var signature = IntegrationsWebApplicationFactory.SignWebhook(body);

        var first = await ReadAsync<JsonElement>(await PostWebhookAsync(body, signature));
        first.GetProperty("wasDuplicate").GetBoolean().Should().BeFalse();

        var second = await ReadAsync<JsonElement>(await PostWebhookAsync(body, signature));
        second.GetProperty("wasDuplicate").GetBoolean().Should().BeTrue();
        second.GetProperty("outcome").GetString().Should().Be("Applied");

        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        (await dbContext
                .Set<Diaspora.Modules.Integrations.Domain.Webhooks.ProcessedIntegrationWebhook>()
                .CountAsync(row => row.ProviderEventId == eventId))
            .Should().Be(1, "the unique index is the replay barrier");
    }

    /// <summary>
    /// A correctly-signed replay from outside the window is refused, and recorded as evidence.
    /// </summary>
    [Fact]
    public async Task A_stale_but_correctly_signed_callback_is_refused_and_recorded()
    {
        var member = await ConnectedMemberAsync();

        var body = $$"""
            {"eventId":"stale-{{Guid.NewGuid():N}}","kind":"grant.revoked",
             "account":"{{member.ExternalAccountId}}","stale":true}
            """;

        var response = await PostWebhookAsync(
            body, IntegrationsWebApplicationFactory.SignWebhook(body));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<JsonElement>(response)).GetProperty("outcome").GetString()
            .Should().Be("Refused");

        await AuthenticateAsync(member.AccessToken);

        (await ReadAsync<JsonElement>(await HttpClient.GetAsync($"{Root}/connections")))
            .EnumerateArray()
            .Single(row => row.GetProperty("provider").GetString() == Calendar)
            .GetProperty("status").GetString().Should().Be("CONNECTED");
    }

    /// <summary>
    /// A callback about an account this deployment does not hold is a 200, not a 4xx.
    /// <para>
    /// Answering 4xx there buys an unbounded redelivery loop against a reference that will never
    /// resolve.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_callback_about_an_unknown_account_is_acknowledged()
    {
        var body = Payload("grant.revoked", $"never-heard-of-{Guid.NewGuid():N}");

        var response = await PostWebhookAsync(
            body, IntegrationsWebApplicationFactory.SignWebhook(body));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<JsonElement>(response)).GetProperty("outcome").GetString()
            .Should().Be("Unmatched");
    }

    /// <summary>A provider with no registered verifier answers 404 and writes nothing.</summary>
    [Fact]
    public async Task A_callback_for_a_provider_with_no_verifier_is_refused()
    {
        var body = Payload("grant.revoked", "whoever");

        using var anonymous = Factory.CreateClient();

        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"{Root}/webhooks/calendar.apple")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        using var response = await anonymous.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// An unrecognised event kind is acknowledged and inert.
    /// <para>
    /// §14: "do not process arbitrary webhook payloads merely because they contain a familiar event
    /// name."
    /// </para>
    /// </summary>
    [Fact]
    public async Task An_unrecognised_event_kind_is_acknowledged_and_does_nothing()
    {
        var member = await ConnectedMemberAsync();
        var body = Payload("account.deleted.forever", member.ExternalAccountId);

        var response = await PostWebhookAsync(
            body, IntegrationsWebApplicationFactory.SignWebhook(body));

        (await ReadAsync<JsonElement>(response)).GetProperty("outcome").GetString()
            .Should().Be("Ignored");

        await AuthenticateAsync(member.AccessToken);

        (await ReadAsync<JsonElement>(await HttpClient.GetAsync($"{Root}/connections")))
            .EnumerateArray()
            .Single(row => row.GetProperty("provider").GetString() == Calendar)
            .GetProperty("status").GetString().Should().Be("CONNECTED");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Capabilities and provider failure.
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The capability report states what this deployment has, and gives a reason for what it has
    /// not.
    /// </summary>
    [Fact]
    public async Task The_capability_report_states_what_is_unavailable_and_why()
    {
        await AuthenticateVerifiedMemberAsync();

        var capabilities = await ReadAsync<JsonElement>(
            await HttpClient.GetAsync($"{Root}/capabilities"));

        capabilities.GetProperty("calendarProviders").EnumerateArray()
            .Select(provider => provider.GetString()).Should().Contain(Calendar);

        capabilities.GetProperty("credentialCustodyConfigured").GetBoolean().Should().BeTrue();
        capabilities.GetProperty("redirectUrisConfigured").GetBoolean().Should().BeTrue();

        capabilities.GetProperty("twoWayCalendarSync").GetBoolean().Should().BeFalse(
            "no adapter declares both import and a watch channel");

        capabilities.GetProperty("unavailableReasons")
            .GetProperty("calendar.sync.twoWay").GetString()
            .Should().NotBeNullOrEmpty("a false without a reason leaves an operator guessing");
    }

    /// <summary>
    /// A provider failure is classified rather than collapsed into a 500.
    /// <para>
    /// §22: the difference is exactly whether a client should offer a retry button.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(IntegrationFailureKind.RateLimited, HttpStatusCode.TooManyRequests)]
    [InlineData(IntegrationFailureKind.Unavailable, HttpStatusCode.ServiceUnavailable)]
    [InlineData(IntegrationFailureKind.InvalidRequest, HttpStatusCode.BadRequest)]
    public async Task A_provider_failure_is_classified_rather_than_collapsed(
        IntegrationFailureKind kind, HttpStatusCode expected)
    {
        await ConnectedMemberAsync();

        Factory.Provider.NextFailure = kind;

        try
        {
            (await HttpClient.GetAsync($"{Root}/calendar/{Calendar}/calendars"))
                .StatusCode.Should().Be(expected);
        }
        finally
        {
            Factory.Provider.NextFailure = null;
        }
    }

    /// <summary>
    /// A provider that refuses the credential ends the grant, and the member is told to reconnect.
    /// </summary>
    [Fact]
    public async Task A_provider_that_refuses_the_credential_ends_the_grant()
    {
        var member = await ConnectedMemberAsync();

        Factory.Provider.NextFailure = IntegrationFailureKind.Unauthorized;

        var response = await HttpClient.GetAsync($"{Root}/calendar/{Calendar}/calendars");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await AuthenticateAsync(member.AccessToken);

        var connection = (await ReadAsync<JsonElement>(
                await HttpClient.GetAsync($"{Root}/connections")))
            .EnumerateArray()
            .Single(row => row.GetProperty("provider").GetString() == Calendar);

        connection.GetProperty("status").GetString().Should().Be("REAUTH_REQUIRED");
        connection.GetProperty("lastError").GetProperty("kind").GetString()
            .Should().Be("ExpiredCredential");
    }

    /// <summary>
    /// The two families on one vendor are separate connections.
    /// <para>
    /// "<c>calendar.apple</c> and <c>identity.apple</c> are different integrations that happen to
    /// share a vendor." Connecting one must not connect the other.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Two_families_on_one_vendor_are_separate_connections()
    {
        await ConnectedMemberAsync();

        var connections = await ReadAsync<JsonElement>(
            await HttpClient.GetAsync($"{Root}/connections"));

        connections.EnumerateArray()
            .Select(row => row.GetProperty("provider").GetString())
            .Should().Contain(Calendar);

        (await HttpClient.GetAsync($"{Root}/calendar/identity.apple/calendars"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest,
                "an identity provider key is not a calendar provider key");
    }

    /// <summary>Every table this module owns lands in its own schema.</summary>
    [Fact]
    public async Task Every_table_lives_in_the_modules_own_schema()
    {
        await AuthenticateVerifiedMemberAsync();

        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var tables = dbContext.Model.GetEntityTypes()
            .Where(entity => entity.ClrType.Namespace?.StartsWith(
                "Diaspora.Modules.Integrations", StringComparison.Ordinal) == true)
            .ToList();

        tables.Should().HaveCount(6);

        tables.Should().AllSatisfy(entity =>
            entity.GetSchema().Should().Be(IntegrationsModuleMetadata.Schema));

        // Written out rather than read from the module's own constants: a test that quoted the
        // same string the mapping quotes would pass through a rename that broke every deployed
        // migration.
        tables.Select(entity => entity.GetTableName()).Should().BeEquivalentTo(
        [
            "integration_connections",
            "integration_oauth_requests",
            "integration_calendar_links",
            "integration_calendar_settings",
            "integration_sync_runs",
            "integration_webhook_events"
        ]);
    }

    /// <summary>
    /// <b>A stored credential is ciphertext, and the plaintext token never reaches the column.</b>
    /// <para>
    /// The one assertion that cannot be made from the API surface, because the API surface correctly
    /// never exposes it. Read from the database directly, which is where a disclosure would happen.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_stored_credential_is_ciphertext_in_the_database()
    {
        var member = await ConnectedMemberAsync();

        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var connection = await dbContext.Set<IntegrationConnection>()
            .FirstAsync(row => row.OwnerId == member.UserId && row.Provider == Calendar);

        var cipher = dbContext.Entry(connection)
            .Property<string?>("_accessTokenCipher").CurrentValue;

        cipher.Should().NotBeNullOrEmpty();
        cipher.Should().NotStartWith("access-",
            "the simulator's tokens are prefixed, so a plaintext write would be visible here");

        connection.CredentialKeyId.Should().NotBeNullOrEmpty(
            "without a key identifier, rotation and total credential loss are the same operation");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // FRONTEND-PHASE16-GAP-017 — the notification catalogue, contributed through B13's own seams.
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>B13 seeds this module's category and templates without referencing this module.</b>
    /// <para>
    /// The claim GAP-017 rests on, and the only one in this wave that depends on discovery rather
    /// than on a call: B13 composes at 1200 and scans every platform assembly for the three
    /// contributor interfaces, so a module composed at 2000 is found. Reasoning says it works;
    /// this says so.
    /// </para>
    /// </summary>
    [Fact]
    public async Task B13_seeds_the_integrations_category_and_its_templates()
    {
        await AuthenticateVerifiedMemberAsync();

        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var category = await dbContext
            .Set<Diaspora.Modules.Notifications.Domain.Catalog.NotificationCategory>()
            .FirstOrDefaultAsync(row => row.Key == "integrations");

        category.Should().NotBeNull("the category is contributed by this module and seeded by B13");
        category!.IsMandatory.Should().BeFalse(
            "a member who switches off 'your grant expires in three days' has made a legitimate "
            + "choice about their own attention");

        var templates = await dbContext
            .Set<Diaspora.Modules.Notifications.Domain.Templates.NotificationTemplate>()
            .Where(row => row.Key.StartsWith("integrations."))
            .ToListAsync();

        templates.Select(template => template.Key).Should().BeEquivalentTo(
        [
            "integrations.grant.reauthorization-required",
            "integrations.grant.expiring",
            "integrations.sync.failed",
            "integrations.disconnected"
        ]);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Fixtures
    // ════════════════════════════════════════════════════════════════════════════════════════

    private sealed record ConnectedMember(Guid UserId, string AccessToken, string ExternalAccountId);

    /// <summary>Registers a verified member, authenticates the client, and returns their id.</summary>
    private async Task<(Guid UserId, string AccessToken)> AuthenticateVerifiedMemberAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();

        Api.Authenticate(auth.Tokens.AccessToken);

        return (auth.User.Id, auth.Tokens.AccessToken);
    }

    private Task AuthenticateAsync(string accessToken)
    {
        Api.Authenticate(accessToken);

        return Task.CompletedTask;
    }

    /// <summary>A verified member with a live calendar connection, over the real routes.</summary>
    private async Task<ConnectedMember> ConnectedMemberAsync(string? externalAccountId = null)
    {
        var (userId, accessToken) = await AuthenticateVerifiedMemberAsync();
        var account = externalAccountId ?? $"account-{Guid.NewGuid():N}";

        var state = await StartAuthorizationAsync(Calendar);

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/oauth/{Calendar}/exchange",
            new
            {
                code = Factory.Provider.IssueCode(account),
                codeVerifier = Verifier,
                state,
                redirectUri = Redirect,
                intent = "link"
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return new ConnectedMember(userId, accessToken, account);
    }

    private async Task<string> StartAuthorizationAsync(string provider)
    {
        var start = await ReadAsync<JsonElement>(await HttpClient.PostAsJsonAsync(
            $"{Root}/oauth/{provider}/authorize",
            new
            {
                redirectUri = Redirect,
                codeChallenge = Challenge(Verifier),
                scopes = new[] { "calendar.read", "calendar.write" },
                intent = "link"
            }));

        return start.GetProperty("state").GetString()!;
    }

    private async Task<HttpResponseMessage> PostCalendarEventAsync(
        object draft, string? idempotencyKey)
    {
        // Awaited before the request is disposed. Returning the task from a method that had
        // already `using`-scoped the message disposes its content mid-flight, which surfaces as an
        // ObjectDisposedException from deep inside the test host and names nothing useful.
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"{Root}/calendar/{Calendar}/events")
        {
            Content = JsonContent.Create(draft)
        };

        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        return await HttpClient.SendAsync(request);
    }

    private static object CalendarDraft(
        string sourceId,
        string title = "Community meeting",
        string? timeZone = "Africa/Lagos",
        string calendarId = "primary") =>
        new
        {
            calendarId,
            sourceId,
            sourceKind = "event",
            title,
            startsAt = "2026-09-01T18:00:00+01:00",
            endsAt = "2026-09-01T20:00:00+01:00",
            timeZone,
            isAllDay = false,
            location = "Lagos",
            reminderMinutes = new[] { 30 }
        };

    private async Task<HttpResponseMessage> PostWebhookAsync(string body, string signature)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"{Root}/webhooks/{Calendar}")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        request.Headers.Add("X-Provider-Signature", signature);

        // Anonymous by design: the signature is the credential, so no bearer token is sent.
        using var anonymous = Factory.CreateClient();

        var response = await anonymous.SendAsync(request);

        // Read here, while the response is still attached to a live handler.
        await response.Content.LoadIntoBufferAsync();

        return response;
    }

    private static string Payload(string kind, string account, string? eventId = null) =>
        $$"""{"eventId":"{{eventId ?? Guid.NewGuid().ToString("N")}}","kind":"{{kind}}","account":"{{account}}"}""";

    private static string Challenge(string verifier) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static readonly JsonSerializerOptions WireJson = new(JsonSerializerDefaults.Web);

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(payload);

        return document.RootElement.GetProperty("data").Deserialize<T>(WireJson)!;
    }

    private static async Task AssertErrorCodeAsync(HttpResponseMessage response, string code)
    {
        var payload = await response.Content.ReadAsStringAsync();

        payload.Should().Contain(code);
    }
}
