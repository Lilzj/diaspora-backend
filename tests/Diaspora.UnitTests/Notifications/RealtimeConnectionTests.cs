using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Extensibility.Realtime;
using Diaspora.Modules.Notifications.Infrastructure.Realtime;
using Diaspora.Modules.Notifications.Options;
using Diaspora.SharedKernel.Identity;
using Diaspora.UnitTests.Common;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diaspora.UnitTests.Notifications;

/// <summary>
/// The connection registry and the presence it produces.
/// <para>
/// <b>Presence is the part of a real-time runtime that fails quietly and stays broken.</b> A missed
/// disconnect leaves somebody permanently online; a socket held open in a pocket makes "online" mean
/// "their phone is charged"; a member with three devices goes offline when they close one. None of
/// those throws, none logs, and all of them are indistinguishable from working until a member
/// complains that the green dot lies.
/// </para>
/// </summary>
public class RealtimeConnectionTests
{
    private static readonly UserId Alice = UserId.From(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly UserId Bob = UserId.From(Guid.Parse("22222222-2222-2222-2222-222222222222"));

    private readonly FakeClock _clock = new();
    private readonly InMemoryRealtimeConnectionRegistry _registry = new();

    [Fact]
    public async Task A_registered_connection_makes_the_member_connected()
    {
        await AddAsync("c1", Alice);

        (await _registry.IsConnectedAsync(Alice)).Should().BeTrue();
        (await _registry.IsConnectedAsync(Bob)).Should().BeFalse();
    }

    [Fact]
    public async Task Connections_are_counted_per_member()
    {
        await AddAsync("c1", Alice);
        await AddAsync("c2", Alice);
        await AddAsync("c3", Bob);

        (await _registry.ForUserAsync(Alice)).Should().HaveCount(2);
        (await _registry.ForUserAsync(Bob)).Should().ContainSingle();
        (await _registry.CountAsync()).Should().Be(3);
    }

    /// <summary>
    /// A socket is routinely reported closed twice — once by the transport and once by a client
    /// saying goodbye. A registry that threw on the second would take the disconnect handler down
    /// with it, and the member would stay online forever.
    /// </summary>
    [Fact]
    public async Task Removing_a_connection_twice_is_not_an_error()
    {
        await AddAsync("c1", Alice);

        await _registry.RemoveAsync("c1");
        await _registry.RemoveAsync("c1");
        await _registry.RemoveAsync("never-existed");

        (await _registry.IsConnectedAsync(Alice)).Should().BeFalse();
        (await _registry.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Closing_one_of_several_connections_leaves_the_member_connected()
    {
        await AddAsync("phone", Alice);
        await AddAsync("tablet", Alice);

        await _registry.RemoveAsync("phone");

        (await _registry.IsConnectedAsync(Alice)).Should().BeTrue(
            "a member who closed one of two devices has not gone offline");
    }

    [Fact]
    public async Task A_member_with_no_connection_is_offline_and_has_no_last_seen()
    {
        var presence = Presence();

        var snapshot = await presence.GetAsync(Alice);

        snapshot.Status.Should().Be(PresenceSnapshot.Offline);
        snapshot.LastSeenAt.Should().BeNull("nobody the platform has never seen has a last-seen time");
        snapshot.ConnectionCount.Should().Be(0);
    }

    [Fact]
    public async Task A_recently_active_member_is_online_and_reports_no_last_seen()
    {
        var presence = Presence();
        await AddAsync("c1", Alice);

        var snapshot = await presence.GetAsync(Alice);

        snapshot.Status.Should().Be(PresenceSnapshot.Online);
        snapshot.ConnectionCount.Should().Be(1);
        snapshot.LastSeenAt.Should().BeNull(
            "'online, last seen 2 seconds ago' is a contradiction a client would render");
    }

    /// <summary>
    /// A phone holds its socket open in a pocket. Reporting that as online is how a green dot stops
    /// meaning anything.
    /// </summary>
    [Fact]
    public async Task An_idle_connection_becomes_away_rather_than_staying_online()
    {
        var presence = Presence(awayAfterSeconds: 60);
        await AddAsync("c1", Alice);

        _clock.Advance(TimeSpan.FromSeconds(61));

        (await presence.GetAsync(Alice)).Status.Should().Be(PresenceSnapshot.Away);
    }

    [Fact]
    public async Task Activity_on_any_device_keeps_the_member_online()
    {
        var presence = Presence(awayAfterSeconds: 60);
        await AddAsync("laptop", Alice);
        await AddAsync("tablet", Alice);

        _clock.Advance(TimeSpan.FromSeconds(61));
        await _registry.TouchAsync("laptop", _clock.UtcNow);

        (await presence.GetAsync(Alice)).Status.Should().Be(
            PresenceSnapshot.Online,
            "somebody typing on a laptop is not away because their tablet is idle");
    }

    [Fact]
    public async Task Going_offline_records_a_last_seen_time()
    {
        var presence = Presence();
        await AddAsync("c1", Alice);
        await _registry.RemoveAsync("c1");

        var disconnectedAt = _clock.UtcNow;
        await presence.RecordAsync(Alice, PresenceState.Offline, disconnectedAt);

        var snapshot = await presence.GetAsync(Alice);

        snapshot.Status.Should().Be(PresenceSnapshot.Offline);
        snapshot.LastSeenAt.Should().Be(disconnectedAt);
    }

    /// <summary>
    /// "Last seen three weeks ago" is disclosure rather than information, and an entry per member who
    /// ever connected is memory the process pays for forever.
    /// </summary>
    [Fact]
    public async Task A_last_seen_time_expires()
    {
        var presence = Presence(lastSeenRetentionMinutes: 60);
        await AddAsync("c1", Alice);
        await _registry.RemoveAsync("c1");
        await presence.RecordAsync(Alice, PresenceState.Offline, _clock.UtcNow);

        _clock.Advance(TimeSpan.FromMinutes(61));

        (await presence.GetAsync(Alice)).LastSeenAt.Should().BeNull();
    }

    [Fact]
    public async Task Coming_back_online_retires_the_last_seen_time()
    {
        var presence = Presence();
        await AddAsync("c1", Alice);
        await _registry.RemoveAsync("c1");
        await presence.RecordAsync(Alice, PresenceState.Offline, _clock.UtcNow);

        await AddAsync("c2", Alice);
        await presence.RecordAsync(Alice, PresenceState.Online, _clock.UtcNow);

        var snapshot = await presence.GetAsync(Alice);

        snapshot.Status.Should().Be(PresenceSnapshot.Online);
        snapshot.LastSeenAt.Should().BeNull();
    }

    /// <summary>
    /// A member with four devices locking one screen must not produce four broadcasts telling
    /// everybody they are still online.
    /// </summary>
    [Fact]
    public async Task Recording_a_state_that_did_not_happen_reports_no_change()
    {
        var presence = Presence();
        await AddAsync("phone", Alice);
        await AddAsync("tablet", Alice);

        var changed = await presence.RecordAsync(Alice, PresenceState.Offline, _clock.UtcNow);

        changed.Should().BeNull(
            "one socket closing while another is open is not the member going offline");
    }

    /// <summary>
    /// Presence is the most privacy-sensitive thing the runtime produces, and a deployment that
    /// switches it off must get silence rather than a second code path everywhere.
    /// </summary>
    [Fact]
    public async Task Presence_switched_off_reports_everybody_offline_without_failing()
    {
        var presence = Presence(enabled: false);
        await AddAsync("c1", Alice);

        var snapshot = await presence.GetAsync(Alice);

        snapshot.Status.Should().Be(PresenceSnapshot.Offline);
        (await presence.RecordAsync(Alice, PresenceState.Online, _clock.UtcNow)).Should().BeNull();
    }

    [Fact]
    public async Task Several_members_are_answered_in_one_call()
    {
        var presence = Presence();
        await AddAsync("c1", Alice);

        var snapshots = await presence.GetManyAsync([Alice, Bob]);

        snapshots.Should().HaveCount(2);
        snapshots.Single(snapshot => snapshot.UserId == Alice).Status.Should().Be(PresenceSnapshot.Online);
        snapshots.Single(snapshot => snapshot.UserId == Bob).Status.Should().Be(PresenceSnapshot.Offline);
    }

    private Task<int> AddAsync(string connectionId, UserId userId) =>
        _registry.AddAsync(new RealtimeConnection(
            connectionId, userId, DevicePlatformKind.IOS, _clock.UtcNow, _clock.UtcNow));

    private InMemoryPresenceStore Presence(
        bool enabled = true, int awayAfterSeconds = 300, int lastSeenRetentionMinutes = 1440)
    {
        var options = new RealtimeOptions
        {
            Presence = new PresenceOptions
            {
                Enabled = enabled,
                AwayAfterSeconds = awayAfterSeconds,
                LastSeenRetentionMinutes = lastSeenRetentionMinutes
            }
        };

        return new InMemoryPresenceStore(
            _registry, new StaticOptionsMonitor<RealtimeOptions>(options), _clock);
    }
}
