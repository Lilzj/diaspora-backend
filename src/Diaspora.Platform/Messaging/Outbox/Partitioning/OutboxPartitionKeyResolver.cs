using Diaspora.Contracts.Events;
using Diaspora.Platform.Messaging.Outbox.Abstractions;
using Diaspora.Platform.Messaging.Outbox.Model;

namespace Diaspora.Platform.Messaging.Outbox.Partitioning;

/// <summary>
/// Runs the registered partition-key strategies in order and guarantees an answer.
/// <para>
/// A chain rather than a single strategy so a module can contribute its own without displacing
/// the convention for every other module's events — the same shape the platform uses for
/// authentication providers and storage backends. First non-null answer wins.
/// </para>
/// <para>
/// <b>The fallback is the event's own identifier, not its type name.</b> A fallback of "type
/// name" would silently serialise every event a strategy failed to classify behind every other
/// event of the same type, and it would look like it was working: messages would be delivered,
/// in order, slowly, and the head-of-line blocking would only be visible under load. Falling
/// back to a unique key means an unclassified event is ordered against nothing, which is the
/// truthful description of what is actually known about it.
/// </para>
/// </summary>
public sealed class OutboxPartitionKeyResolver
{
    private readonly IReadOnlyList<IOutboxPartitionKeyStrategy> _strategies;

    public OutboxPartitionKeyResolver(IEnumerable<IOutboxPartitionKeyStrategy> strategies)
    {
        ArgumentNullException.ThrowIfNull(strategies);
        _strategies = strategies.ToArray();
    }

    public string Resolve(IIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        foreach (var strategy in _strategies)
        {
            var key = strategy.Resolve(integrationEvent);

            if (!string.IsNullOrWhiteSpace(key))
            {
                return Clamp(key);
            }
        }

        return Clamp($"event:{integrationEvent.EventId:N}");
    }

    /// <summary>
    /// Keeps the key inside its column. Truncated from the left, so the discriminating end of a
    /// long key survives — a key built from a namespace-heavy type name differs only in its
    /// tail, and truncating from the right would map every one of them onto the same partition.
    /// </summary>
    private static string Clamp(string key) =>
        key.Length <= OutboxFieldLimits.PartitionKey
            ? key
            : key[^OutboxFieldLimits.PartitionKey..];
}
