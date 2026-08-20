using Diaspora.Contracts.Events;
using Diaspora.Platform.Messaging.Outbox.Abstractions;

namespace Diaspora.UnitTests.Platform.Outbox;

/// <summary>
/// A strategy that answers the same key for everything.
/// <para>
/// Stands in for the module-contributed strategy the seam exists to support, and is the
/// cheapest way to prove the chain honours order of registration rather than quietly always
/// running the convention.
/// </para>
/// </summary>
public sealed class FixedOutboxPartitionKeyStrategy : IOutboxPartitionKeyStrategy
{
    private readonly string _key;

    public FixedOutboxPartitionKeyStrategy(string key) => _key = key;

    public string? Resolve(IIntegrationEvent integrationEvent) => _key;
}
