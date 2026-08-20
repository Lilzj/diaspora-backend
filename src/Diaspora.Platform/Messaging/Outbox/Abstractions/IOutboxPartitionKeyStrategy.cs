using Diaspora.Contracts.Events;

namespace Diaspora.Platform.Messaging.Outbox.Abstractions;

/// <summary>
/// Decides which ordering partition an event belongs to.
/// <para>
/// The partition key is the unit of ordering: messages sharing one are delivered in write order
/// and never concurrently, and messages in different partitions are independent. Choosing it
/// well is choosing where the platform pays for ordering — too coarse and one failing message
/// stalls a whole module, too fine and events about the same thing arrive out of order.
/// </para>
/// <para>
/// A module that needs different grouping registers its own implementation through the
/// contributor model rather than editing the default. That is deliberate: the point of B1.1 is
/// that reliable messaging is configurable without any module changing, and the same must hold
/// for the modules that want something other than the convention.
/// </para>
/// </summary>
public interface IOutboxPartitionKeyStrategy
{
    /// <summary>
    /// Returns the partition key for an event, or null to fall back to the next strategy and
    /// ultimately to the platform default.
    /// </summary>
    string? Resolve(IIntegrationEvent integrationEvent);
}
