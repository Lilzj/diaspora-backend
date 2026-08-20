using Diaspora.Modules.Notifications.Observability;
using Diaspora.Platform.Communication;
using Diaspora.SharedKernel.Identity;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Notifications.Infrastructure.Realtime;

/// <summary>
/// Which module owns which channel family, resolved once, with every failure answered "no".
/// <para>
/// <b>Two contributors claiming one prefix is a startup failure, not a race.</b> Silently picking the
/// first would mean a deployment that accidentally registered a second authorizer for
/// <c>conversation:</c> admitted members through whichever the container happened to order first —
/// a difference nobody would find until somebody read a thread they were not in.
/// </para>
/// <para>
/// <b>A channel whose prefix nobody claims is refused.</b> That is the shipped answer for every name
/// a client can invent, and it is why a hub can accept an arbitrary string from a socket without the
/// string being able to do anything.
/// </para>
/// <para>
/// <b>Presence unions and joins do not.</b> Joining is a question about one channel, so exactly one
/// contributor answers it. Presence is a question about a person, and sharing a conversation and
/// sharing an organization are independent reasons to be permitted — so every contributor is asked
/// and the first yes wins. Getting that backwards would either leak availability to whoever owned
/// the first prefix, or hide it from everybody.
/// </para>
/// </summary>
public sealed class RealtimeChannelRegistry
{
    private readonly Dictionary<string, IRealtimeChannelAuthorizer> _byPrefix;
    private readonly IReadOnlyList<IRealtimeChannelAuthorizer> _all;
    private readonly ILogger<RealtimeChannelRegistry> _logger;

    public RealtimeChannelRegistry(
        IEnumerable<IRealtimeChannelAuthorizer> authorizers,
        ILogger<RealtimeChannelRegistry> logger)
    {
        ArgumentNullException.ThrowIfNull(authorizers);

        _logger = logger;
        _all = [.. authorizers];
        _byPrefix = new Dictionary<string, IRealtimeChannelAuthorizer>(StringComparer.Ordinal);

        foreach (var authorizer in _all)
        {
            var prefix = authorizer.ChannelPrefix;

            if (string.IsNullOrWhiteSpace(prefix)
                || prefix[^1] != RealtimeChannels.Separator
                || prefix.Length < 2)
            {
                throw new InvalidOperationException(
                    $"'{authorizer.GetType().FullName}' claims the real-time channel prefix "
                    + $"'{prefix}', which does not end with '{RealtimeChannels.Separator}'. A prefix "
                    + "without its separator also claims every longer word that starts with it.");
            }

            if (!_byPrefix.TryAdd(prefix, authorizer))
            {
                throw new InvalidOperationException(
                    $"Two modules claim the real-time channel prefix '{prefix}': "
                    + $"'{_byPrefix[prefix].Module}' and '{authorizer.Module}'. Exactly one may own a "
                    + "channel family, because the owner is what decides who may listen to it.");
            }
        }
    }

    /// <summary>Whether any module has claimed a channel family. Reported on the handshake.</summary>
    public bool IsOwned(string prefix) =>
        !string.IsNullOrWhiteSpace(prefix) && _byPrefix.ContainsKey(prefix);

    /// <summary>Every claimed prefix. For the health contributor and the startup validator.</summary>
    public IReadOnlyCollection<string> OwnedPrefixes => _byPrefix.Keys;

    /// <summary>
    /// Whether a member may join a channel. False for anything unclaimed, malformed or failing.
    /// </summary>
    public async Task<bool> CanJoinAsync(
        UserId userId, string channel, CancellationToken cancellationToken = default)
    {
        if (userId is null || string.IsNullOrWhiteSpace(channel))
        {
            return false;
        }

        if (Owner(channel) is not { } authorizer)
        {
            NotificationTelemetry.RealtimeJoinsRefused.Add(
                1, new KeyValuePair<string, object?>("reason", "unclaimed"));

            return false;
        }

        var permitted = await SafelyAsync(
            () => authorizer.CanJoinAsync(userId, channel, cancellationToken),
            authorizer,
            nameof(IRealtimeChannelAuthorizer.CanJoinAsync));

        if (!permitted)
        {
            NotificationTelemetry.RealtimeJoinsRefused.Add(
                1, new KeyValuePair<string, object?>("reason", "denied"));
        }

        return permitted;
    }

    /// <summary>
    /// The members a message to this channel should reach, capped by the caller.
    /// <para>
    /// An unclaimed or failing channel yields nobody rather than everybody — see the seam's own
    /// documentation for why that is the safe direction for a fan-out too.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<UserId>> ListSubscribersAsync(
        string channel, int limit, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(channel) || limit <= 0 || Owner(channel) is not { } authorizer)
        {
            return [];
        }

        try
        {
            var subscribers = await authorizer.ListSubscribersAsync(channel, limit, cancellationToken);

            return subscribers.Count <= limit ? subscribers : [.. subscribers.Take(limit)];
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // A fan-out that cannot be resolved reaches nobody. See the summary.
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Module '{Module}' could not list subscribers for a real-time channel. Nobody was "
                + "addressed.",
                authorizer.Module);

            return [];
        }
#pragma warning restore CA1031
    }

    /// <summary>Whether any contributor permits one member to observe another's availability.</summary>
    public async Task<bool> CanObservePresenceAsync(
        UserId observer, UserId subject, CancellationToken cancellationToken = default)
    {
        if (observer is null || subject is null)
        {
            return false;
        }

        foreach (var authorizer in _all)
        {
            var permitted = await SafelyAsync(
                () => authorizer.CanObservePresenceAsync(observer, subject, cancellationToken),
                authorizer,
                nameof(IRealtimeChannelAuthorizer.CanObservePresenceAsync));

            if (permitted)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The contributor that owns a channel, or null when nobody claimed its prefix.</summary>
    private IRealtimeChannelAuthorizer? Owner(string channel) =>
        RealtimeChannels.PrefixOf(channel) is { } prefix
            ? _byPrefix.GetValueOrDefault(prefix)
            : null;

    /// <summary>
    /// Runs one contributor's answer and treats a throw as a refusal.
    /// <para>
    /// A contributor reads a table, and a table can be unreachable. Propagating would turn a
    /// transient database fault into an error frame the client cannot tell apart from a server bug,
    /// and would do it on a path whose only correct failure mode is "no".
    /// </para>
    /// </summary>
    private async Task<bool> SafelyAsync(
        Func<Task<bool>> question, IRealtimeChannelAuthorizer authorizer, string operation)
    {
        try
        {
            return await question();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // An authorisation question answers "no" on failure. See the summary.
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Module '{Module}' could not answer {Operation} for a real-time channel. Refusing.",
                authorizer.Module,
                operation);

            return false;
        }
#pragma warning restore CA1031
    }
}
