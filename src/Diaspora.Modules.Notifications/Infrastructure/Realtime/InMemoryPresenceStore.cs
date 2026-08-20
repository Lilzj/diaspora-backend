using System.Collections.Concurrent;
using Diaspora.Modules.Notifications.Extensibility.Realtime;
using Diaspora.Modules.Notifications.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Notifications.Infrastructure.Realtime;

/// <summary>
/// Availability, derived from live sockets and remembered briefly after they close.
/// <para>
/// <b>The current answer is computed from the connection registry rather than stored, and the
/// stored half is only the past.</b> Keeping a written "is online" flag means every crash, every
/// pod eviction and every missed disconnect leaves somebody permanently online — the single most
/// common presence bug there is. What cannot be recomputed is when somebody was last here, so that
/// is the only thing this remembers.
/// </para>
/// <para>
/// <b>Away is idleness, not a client's claim.</b> A phone holds its socket open in a pocket, so
/// "has a connection" and "is paying attention" are different facts. The threshold is
/// <c>Presence:AwayAfterSeconds</c> and it is applied to the most recent activity across <em>all</em>
/// of a member's sockets — somebody typing on a laptop is not away because their tablet is idle.
/// </para>
/// <para>
/// <b>Last-seen entries expire.</b> "Last seen 3 weeks ago" is disclosure rather than information,
/// and an unbounded dictionary keyed by every member who ever connected is memory this instance
/// pays for forever. Expiry is checked on read, which costs nothing on the path that matters and
/// avoids a timer whose only job is to delete.
/// </para>
/// </summary>
public sealed class InMemoryPresenceStore : IPresenceStore
{
    private readonly IRealtimeConnectionRegistry _connections;
    private readonly IOptionsMonitor<RealtimeOptions> _options;
    private readonly IClock _clock;

    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _lastSeen = new();

    public InMemoryPresenceStore(
        IRealtimeConnectionRegistry connections,
        IOptionsMonitor<RealtimeOptions> options,
        IClock clock)
    {
        _connections = connections;
        _options = options;
        _clock = clock;
    }

    public async Task<PresenceSnapshot> GetAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var options = _options.CurrentValue;

        if (!options.Presence.Enabled)
        {
            // Presence switched off answers "offline, and we are not saying when" rather than
            // failing. A client renders no dot, which is the intended product behaviour, and no
            // caller needs a second code path.
            return PresenceSnapshot.Absent(userId);
        }

        var live = await _connections.ForUserAsync(userId, cancellationToken);

        if (live.Count == 0)
        {
            return PresenceSnapshot.Absent(userId, LastSeen(userId.Value, options));
        }

        var now = _clock.UtcNow;
        var mostRecent = live.Max(connection => connection.LastActivityAt);
        var idleFor = now - mostRecent;

        var state = idleFor.TotalSeconds >= options.Presence.AwayAfterSeconds
            ? PresenceState.Away
            : PresenceState.Online;

        return new PresenceSnapshot(userId, PresenceSnapshot.Spell(state), null, live.Count);
    }

    public async Task<IReadOnlyList<PresenceSnapshot>> GetManyAsync(
        IReadOnlyCollection<UserId> userIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        var snapshots = new List<PresenceSnapshot>(userIds.Count);

        foreach (var userId in userIds)
        {
            snapshots.Add(await GetAsync(userId, cancellationToken));
        }

        return snapshots;
    }

    public async Task<PresenceSnapshot?> RecordAsync(
        UserId userId,
        PresenceState state,
        DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        if (!_options.CurrentValue.Presence.Enabled)
        {
            return null;
        }

        if (state == PresenceState.Offline)
        {
            _lastSeen[userId.Value] = at;
        }
        else
        {
            // Coming back online retires the last-seen entry rather than updating it. Holding both
            // lets a race report somebody as online *and* last seen a moment ago, which is the
            // contradiction PresenceSnapshot exists to prevent.
            _lastSeen.TryRemove(userId.Value, out _);
        }

        var snapshot = await GetAsync(userId, cancellationToken);

        // A disconnect that leaves other sockets open is not a change worth broadcasting: the member
        // is still online, and telling every observer so would turn a member with four devices into
        // four broadcasts per screen lock.
        var expected = PresenceSnapshot.Spell(state);

        return snapshot.Status == expected ? snapshot : null;
    }

    private DateTimeOffset? LastSeen(Guid userId, RealtimeOptions options)
    {
        if (!_lastSeen.TryGetValue(userId, out var at))
        {
            return null;
        }

        if (_clock.UtcNow - at <= TimeSpan.FromMinutes(options.Presence.LastSeenRetentionMinutes))
        {
            return at;
        }

        _lastSeen.TryRemove(userId, out _);

        return null;
    }
}
