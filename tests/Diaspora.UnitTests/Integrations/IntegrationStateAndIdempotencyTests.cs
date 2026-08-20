using Diaspora.Modules.Integrations.Application.Mapping;
using Diaspora.Modules.Integrations.Domain.Calendar;
using Diaspora.Modules.Integrations.Domain.Connections;
using Diaspora.Modules.Integrations.Domain.Enums;
using Diaspora.Modules.Integrations.Domain.Identifiers;
using Diaspora.Modules.Integrations.Domain.Providers;
using Diaspora.Modules.Integrations.Domain.Sync;
using Diaspora.Modules.Integrations.Domain.Webhooks;
using Diaspora.Modules.Integrations.Extensibility.Providers;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Integrations;

/// <summary>
/// Duplicate suppression, phase derivation and the status the client renders.
/// <para>
/// <b>Three families of defect that are invisible in a passing integration test.</b> A fingerprint
/// that changed on every call would rewrite every exported entry nightly and nobody would notice
/// until a provider's quota complained. A phase derived from a caller's opinion rather than from the
/// counts would let a run report <c>synced</c> having written nothing. And a status composed wrongly
/// would show <c>CONNECTED</c> for a grant that had been revoked a week earlier — which is precisely
/// the failure the register asks the webhook infrastructure to exist for.
/// </para>
/// </summary>
public sealed class IntegrationStateAndIdempotencyTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 12, 10, 0, 0, TimeSpan.Zero);

    // ── Calendar fingerprinting ─────────────────────────────────────────────────────────────

    /// <summary>The same event fingerprints the same way, so a re-export is a no-op.</summary>
    [Fact]
    public void An_unchanged_event_produces_the_same_fingerprint()
    {
        Fingerprint().Should().Be(Fingerprint());
    }

    /// <summary>
    /// Every field a provider would see changes the fingerprint.
    /// <para>
    /// A field that did not would be a field whose edit is silently never exported — the member
    /// changes the location, the sync says "already written", and the calendar keeps the old one.
    /// </para>
    /// </summary>
    [Fact]
    public void Every_exported_field_changes_the_fingerprint()
    {
        var baseline = Fingerprint();

        Fingerprint(title: "Different").Should().NotBe(baseline);
        Fingerprint(startsAt: Now.AddMinutes(1)).Should().NotBe(baseline);
        Fingerprint(endsAt: Now.AddHours(3)).Should().NotBe(baseline);
        Fingerprint(timeZone: "Europe/London").Should().NotBe(baseline);
        Fingerprint(isAllDay: true).Should().NotBe(baseline);
        Fingerprint(location: "Elsewhere").Should().NotBe(baseline);
        Fingerprint(notes: "Changed").Should().NotBe(baseline);
        Fingerprint(url: "https://diaspora.test/other").Should().NotBe(baseline);
        Fingerprint(reminders: [10, 30]).Should().NotBe(baseline);
        Fingerprint(recurrence: "RRULE:FREQ=DAILY").Should().NotBe(baseline);
    }

    /// <summary>
    /// Reminder order does not change the fingerprint, because it does not change the entry.
    /// <para>
    /// A client that sends the same reminders in a different order would otherwise trigger a
    /// pointless provider write on every sync.
    /// </para>
    /// </summary>
    [Fact]
    public void Reminder_order_does_not_change_the_fingerprint()
    {
        Fingerprint(reminders: [10, 60]).Should().Be(Fingerprint(reminders: [60, 10]));
    }

    /// <summary>
    /// Two fields cannot be confused by concatenation.
    /// <para>
    /// A naive join would make title "Standup" + location "Friday" hash identically to title
    /// "StandupFriday" + no location, which would silently suppress a legitimate update.
    /// </para>
    [Fact]
    public void Adjacent_fields_cannot_be_confused_by_concatenation()
    {
        Fingerprint(title: "Standup", location: "Friday")
            .Should().NotBe(Fingerprint(title: "StandupFriday", location: null));
    }

    /// <summary>
    /// A link replays the recorded outcome for the same idempotency key, and only for that key.
    /// </summary>
    [Fact]
    public void A_link_recognises_the_attempt_that_wrote_it()
    {
        var link = NewLink(idempotencyKey: "attempt-1");

        link.WasWrittenBy("attempt-1").Should().BeTrue();
        link.WasWrittenBy("attempt-2").Should().BeFalse();
        link.WasWrittenBy(null).Should().BeFalse();
    }

    /// <summary>
    /// The idempotency key is hashed, not stored.
    /// <para>
    /// A client is free to put anything in that header, and this platform should not become the
    /// place where it is retained in the clear.
    /// </para>
    /// </summary>
    [Fact]
    public void The_idempotency_key_is_stored_as_a_hash()
    {
        var link = NewLink(idempotencyKey: "attempt-1");

        link.IdempotencyKey.Should().NotBe("attempt-1");
        link.IdempotencyKey.Should().HaveLength(CalendarEventLink.IdempotencyKeyLength);
    }

    /// <summary>A removed link no longer matches a fingerprint, so a re-export writes again.</summary>
    [Fact]
    public void A_removed_link_does_not_suppress_a_re_export()
    {
        var link = NewLink();
        var fingerprint = link.ContentHash;

        link.Matches(fingerprint).Should().BeTrue();

        link.RecordRemoval(Now);

        link.Matches(fingerprint).Should().BeFalse();
        link.State.Should().Be(CalendarLinkState.Removed);
    }

    /// <summary>
    /// A member deleting an exported entry themselves is recorded distinctly from Diaspora removing
    /// it — which is what stops a sync putting it back.
    /// </summary>
    [Fact]
    public void An_entry_the_member_deleted_is_recorded_as_removed_at_the_provider()
    {
        var link = NewLink();

        link.RecordRemovalAtProvider(Now);

        link.State.Should().Be(CalendarLinkState.RemovedAtProvider);
        link.State.Should().NotBe(CalendarLinkState.Removed);
    }

    // ── Sync phase ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The phase is derived from the counts and cannot be supplied.
    /// <para>
    /// <b>The single place a synchronisation could claim a success it did not produce.</b> A caller
    /// able to pass <c>synced</c> alongside a non-zero failure count is a caller that can tell a
    /// member their calendar is up to date when it is not.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(5, 0, SyncPhase.Synced)]
    [InlineData(0, 0, SyncPhase.Synced)]
    [InlineData(5, 2, SyncPhase.Partial)]
    [InlineData(0, 3, SyncPhase.Failed)]
    public void The_sync_phase_is_derived_from_what_actually_happened(
        int synced, int failed, SyncPhase expected)
    {
        var run = NewRun();

        run.Complete(
            synced,
            [.. Enumerable.Range(0, failed).Select(index => $"reason {index}")],
            needsAttention: false,
            Now);

        run.Phase.Should().Be(expected);
        run.ItemsSynced.Should().Be(synced);
        run.ItemsFailed.Should().Be(failed);
    }

    /// <summary>Failure reasons are deduplicated, so twelve identical failures read as one sentence.</summary>
    [Fact]
    public void Identical_failure_reasons_are_reported_once()
    {
        var run = NewRun();

        run.Complete(1, ["that calendar is read-only", "that calendar is read-only"], false, Now);

        run.FailureReasons.Should().ContainSingle();
        run.ItemsFailed.Should().Be(1);
    }

    /// <summary>
    /// A run nobody finished is closed as failed with its outcome stated as unknown — never as a
    /// partial success with an invented count.
    /// </summary>
    [Fact]
    public void An_abandoned_run_is_closed_as_failed_with_no_invented_counts()
    {
        var run = NewRun();

        run.Abandon(Now.AddHours(1));

        run.Phase.Should().Be(SyncPhase.Failed);
        run.ItemsSynced.Should().Be(0);
        run.NeedsAttention.Should().BeFalse("a crashed worker is not something a member can act on");
        run.FailureReasons.Should().ContainSingle();
    }

    /// <summary>Abandoning a terminal run changes nothing — the reaper must be re-runnable.</summary>
    [Fact]
    public void Abandoning_a_completed_run_does_nothing()
    {
        var run = NewRun();
        run.Complete(3, [], needsAttention: false, Now);

        run.Abandon(Now.AddHours(2));

        run.Phase.Should().Be(SyncPhase.Synced);
        run.ItemsSynced.Should().Be(3);
    }

    /// <summary>A run stops being the concurrency barrier once it goes stale.</summary>
    [Fact]
    public void A_run_stops_being_live_once_it_is_stale()
    {
        var run = NewRun();

        run.IsLive(Now).Should().BeTrue();
        run.IsLive(Now + IntegrationSyncRun.StaleAfter).Should().BeFalse();
    }

    // ── Status composition ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// A revoked grant reads as <c>REAUTH_REQUIRED</c> whatever a sync run says.
    /// <para>
    /// The ordering that matters most: a <c>SYNCING</c> badge over a dead grant is the most
    /// misleading pair this surface can produce.
    /// </para>
    /// </summary>
    [Fact]
    public void A_grant_needing_the_member_outranks_a_running_sync()
    {
        var connection = Connected();
        connection.RequireReauthorization("gone", Now);

        var running = NewRun();

        IntegrationMapper.ToStatus(connection, running, providerConfigured: true)
            .Should().Be(IntegrationMapper.StatusReauthRequired);
    }

    /// <summary>
    /// A row that has never held a grant reads as never-connected, not as disconnected.
    /// <para>
    /// The two look identical in the database and mean different things to a member.
    /// </para>
    /// </summary>
    [Fact]
    public void A_connection_that_never_held_a_grant_reads_as_not_connected()
    {
        IntegrationMapper.ToStatus(NewConnection(), latestSync: null, providerConfigured: true)
            .Should().Be(IntegrationMapper.StatusNotConnected);
    }

    /// <summary>A stored grant whose adapter has gone reads as unavailable, not connected.</summary>
    [Fact]
    public void A_grant_whose_adapter_is_gone_reads_as_unavailable()
    {
        IntegrationMapper.ToStatus(Connected(), latestSync: null, providerConfigured: false)
            .Should().Be(IntegrationMapper.StatusUnavailable);
    }

    /// <summary>
    /// <c>SYNCED</c> requires a confirmed completion, and degrades to <c>CONNECTED</c> without one.
    /// <para>
    /// The same guard the client applies to itself: "phase 'synced' with a null lastSyncedAt
    /// produces CONNECTED, so an adapter that forgot the timestamp degrades to the weaker, true
    /// claim."
    /// </para>
    /// </summary>
    [Fact]
    public void Synced_requires_a_confirmed_completion()
    {
        var completed = NewRun();
        completed.Complete(2, [], needsAttention: false, Now);

        IntegrationMapper.ToStatus(Connected(), completed, providerConfigured: true)
            .Should().Be(IntegrationMapper.StatusSynced);

        IntegrationMapper.ToStatus(Connected(), NewRun(), providerConfigured: true)
            .Should().Be(IntegrationMapper.StatusSyncing);
    }

    /// <summary>
    /// A failed run never produces a <c>lastSyncedAt</c>.
    /// <para>
    /// It has a <c>completedAt</c>, and presenting that as the moment a sync succeeded is how a
    /// member comes to believe their calendar is current.
    /// </para>
    /// </summary>
    [Fact]
    public void A_failed_run_reports_no_last_synced_moment()
    {
        var run = NewRun();
        run.Fail("the grant has ended", needsAttention: true, Now);

        var state = IntegrationMapper.ToSyncState(run);

        state.Phase.Should().Be("failed");
        state.LastSyncedAt.Should().BeNull();
        state.NeedsAttention.Should().BeTrue();
    }

    /// <summary>
    /// No run at all is <c>never</c>, not a zeroed <c>synced</c>.
    /// <para>
    /// "A synchronisation that has never happened" and "one that happened and moved nothing" render
    /// identically if this returns the wrong one, and they are opposite claims.
    /// </para>
    /// </summary>
    [Fact]
    public void No_run_reports_never_rather_than_an_empty_success()
    {
        var state = IntegrationMapper.ToSyncState(null);

        state.Phase.Should().Be("never");
        state.LastSyncedAt.Should().BeNull();
        state.SyncId.Should().BeNull();
    }

    /// <summary>
    /// Capabilities are intersected with the adapter's, never assumed from the grant.
    /// <para>
    /// An adapter that cannot write must not produce a connection advertising
    /// <c>calendar.event.create</c>, and two-way sync requires both halves — the register's rule
    /// that until a watch channel exists nothing may claim it.
    /// </para>
    /// </summary>
    [Fact]
    public void Capabilities_come_from_the_adapter_and_two_way_needs_both_halves()
    {
        var exportOnly = IntegrationMapper.ToCapabilities(
            Connected(),
            providerConfigured: true,
            [ExternalCalendarCapability.List, ExternalCalendarCapability.CreateEvent]);

        exportOnly.Should().Contain("calendar.list")
            .And.Contain("calendar.event.create")
            .And.Contain("calendar.sync.oneWay")
            .And.NotContain("calendar.sync.twoWay")
            .And.NotContain("calendar.event.delete");

        var importOnly = IntegrationMapper.ToCapabilities(
            Connected(), providerConfigured: true, [ExternalCalendarCapability.ImportEvents]);

        importOnly.Should().NotContain("calendar.sync.twoWay",
            "import without a watch channel is not two-way sync");

        var both = IntegrationMapper.ToCapabilities(
            Connected(),
            providerConfigured: true,
            [ExternalCalendarCapability.ImportEvents, ExternalCalendarCapability.WatchChannel]);

        both.Should().Contain("calendar.sync.twoWay");
    }

    /// <summary>An unusable grant reports no capabilities at all.</summary>
    [Fact]
    public void An_unusable_grant_reports_no_capabilities()
    {
        IntegrationMapper.ToCapabilities(
                NewConnection(),
                providerConfigured: true,
                [ExternalCalendarCapability.List, ExternalCalendarCapability.CreateEvent])
            .Should().BeEmpty();
    }

    // ── Webhook replay key ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// A provider that sends no event id still gets replay protection, from the body.
    /// <para>
    /// Weaker than a real event id and strictly better than nothing, which is the alternative. The
    /// stored value is a hash, never the body — that row is read by support.
    /// </para>
    /// </summary>
    [Fact]
    public void A_body_derived_event_id_is_stable_and_is_not_the_body()
    {
        const string body = """{"kind":"grant.revoked","account":"a"}""";

        var derived = ProcessedIntegrationWebhook.DeriveEventId(body);

        derived.Should().Be(ProcessedIntegrationWebhook.DeriveEventId(body));
        derived.Should().StartWith("body:").And.NotContain("grant.revoked");
        derived.Should().NotBe(ProcessedIntegrationWebhook.DeriveEventId(body + " "));
    }

    /// <summary>Provider names are normalised, because a URL segment is not case-stable.</summary>
    [Fact]
    public void Provider_names_are_normalised_before_storage()
    {
        ProcessedIntegrationWebhook.NormalizeProvider("  Calendar.Google  ")
            .Should().Be("calendar.google");
    }

    // ── Fixtures ────────────────────────────────────────────────────────────────────────────

    private static string Fingerprint(
        string title = "Community meeting",
        DateTimeOffset? startsAt = null,
        DateTimeOffset? endsAt = null,
        string timeZone = "Africa/Lagos",
        bool isAllDay = false,
        string? location = "Lagos",
        string? notes = "Bring the agenda",
        string? url = "https://diaspora.test/events/1",
        IReadOnlyList<int>? reminders = null,
        string? recurrence = null) =>
        CalendarEventLink.Fingerprint(
            title,
            startsAt ?? Now,
            endsAt ?? Now.AddHours(2),
            timeZone,
            isAllDay,
            location,
            notes,
            url,
            reminders ?? [30],
            recurrence);

    private static CalendarEventLink NewLink(string? idempotencyKey = "attempt-1") =>
        CalendarEventLink.Record(
            CalendarEventLinkId.New(),
            IntegrationConnectionId.New(),
            Guid.NewGuid(),
            "primary",
            "event",
            "event-1",
            "external-1",
            "Africa/Lagos",
            Fingerprint(),
            idempotencyKey,
            viewUrl: null,
            Now);

    private static IntegrationSyncRun NewRun() =>
        IntegrationSyncRun.Start(
            IntegrationSyncRunId.New(),
            IntegrationConnectionId.New(),
            Guid.NewGuid(),
            "calendar.google",
            "primary",
            SyncDirection.Export,
            Now);

    private static IntegrationConnection NewConnection() =>
        IntegrationConnection.Start(
            IntegrationConnectionId.New(),
            IntegrationOwnerKind.User,
            Guid.NewGuid(),
            ProviderKey.Parse("calendar.google"),
            Now);

    private static IntegrationConnection Connected()
    {
        var connection = NewConnection();

        connection.ApplyGrant(
            new ExternalAccount("account-1", "amara@example.test"),
            ["calendar.read"],
            "cipher:access",
            "cipher:refresh",
            "key-v1",
            Now.AddHours(1),
            Now);

        return connection;
    }
}
