using System.Collections.Concurrent;
using Diaspora.Modules.Notifications.Extensibility.Realtime;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Infrastructure.Realtime;

/// <summary>
/// Tracks this instance's live sockets in memory.
/// <para>
/// <b>Correct and complete for one process, and it answers only for one process.</b> That is stated
/// plainly rather than hidden behind a hopeful abstraction: a two-instance deployment running this
/// registry reports half its members offline, and the way that is caught is
/// <c>Realtime:InstanceCount</c> plus a startup validator, not a cleverer dictionary. Replacing this
/// with a shared-store implementation is one registration.
/// </para>
/// <para>
/// <b>Two indexes rather than one, because both questions are on hot paths.</b> Disconnect arrives
/// with a connection id and nothing else; fan-out and presence arrive with a user id and nothing
/// else. Scanning either way would be a lock held across every socket on the instance, at the exact
/// moment a deploy reconnects all of them at once.
/// </para>
/// <para>
/// <b>Removal is idempotent by contract.</b> A socket is routinely reported closed twice — once by
/// the transport and once by a client saying goodbye — and a registry that threw on the second would
/// take the disconnect handler down with it.
/// </para>
/// </summary>
public sealed class InMemoryRealtimeConnectionRegistry : IRealtimeConnectionRegistry
{
    private readonly ConcurrentDictionary<string, RealtimeConnection> _byConnection =
        new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>> _byUser = new();

    public Task<int> AddAsync(
        RealtimeConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        _byConnection[connection.ConnectionId] = connection;

        var connections = _byUser.GetOrAdd(
            connection.UserId.Value, _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal));

        connections[connection.ConnectionId] = 0;

        return Task.FromResult(connections.Count);
    }

    public Task RemoveAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(connectionId) || !_byConnection.TryRemove(connectionId, out var removed))
        {
            return Task.CompletedTask;
        }

        if (_byUser.TryGetValue(removed.UserId.Value, out var connections))
        {
            connections.TryRemove(connectionId, out _);

            // The empty bucket is dropped so a process that has served a million members over a week
            // is not holding a million empty dictionaries. TryRemove on the pair rather than on the
            // key: a connection added between the count and the removal must not lose its bucket.
            if (connections.IsEmpty)
            {
                _byUser.TryRemove(
                    new KeyValuePair<Guid, ConcurrentDictionary<string, byte>>(
                        removed.UserId.Value, connections));
            }
        }

        return Task.CompletedTask;
    }

    public Task TouchAsync(
        string connectionId, DateTimeOffset at, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(connectionId)
            && _byConnection.TryGetValue(connectionId, out var existing))
        {
            _byConnection.TryUpdate(
                connectionId, existing with { LastActivityAt = at }, existing);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RealtimeConnection>> ForUserAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        if (!_byUser.TryGetValue(userId.Value, out var connections))
        {
            return Task.FromResult<IReadOnlyList<RealtimeConnection>>([]);
        }

        var resolved = new List<RealtimeConnection>(connections.Count);

        foreach (var connectionId in connections.Keys)
        {
            // A connection can close between enumerating the index and reading the record. A miss is
            // ordinary rather than a corruption, and dropping it is the whole handling.
            if (_byConnection.TryGetValue(connectionId, out var connection))
            {
                resolved.Add(connection);
            }
        }

        return Task.FromResult<IReadOnlyList<RealtimeConnection>>(resolved);
    }

    public Task<bool> IsConnectedAsync(UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return Task.FromResult(
            _byUser.TryGetValue(userId.Value, out var connections) && !connections.IsEmpty);
    }

    public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_byConnection.Count);
}
