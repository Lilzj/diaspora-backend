using System.Collections.Concurrent;
using System.Reflection;
using Diaspora.Contracts.Events;
using Diaspora.Platform.Messaging.Outbox.Abstractions;

namespace Diaspora.Platform.Messaging.Outbox.Partitioning;

/// <summary>
/// Derives the ordering partition from the subject the event is about.
/// <para>
/// <b>The convention is the one every contract in this platform already follows.</b>
/// <c>ProviderPublishedIntegrationEvent</c> carries a <c>ProviderId</c>;
/// <c>VerificationApprovedIntegrationEvent</c> carries a <c>VerificationId</c>. Matching the
/// event's own subject noun against its properties finds the identifier that names what the
/// event is about — which is exactly the grain at which ordering matters, because two events
/// about the same provider must arrive in order and two events about different providers have
/// no relationship at all.
/// </para>
/// <para>
/// <b>Why not simply partition by event type.</b> It would be one line and it would be wrong in
/// both directions: every <c>ProviderUpdated</c> in the platform would serialise behind every
/// other, and a <c>ProviderUpdated</c> would still be free to overtake the
/// <c>ProviderCreated</c> for the same provider. Coarser ordering that still fails to order the
/// thing anyone cares about is the worst of both.
/// </para>
/// <para>
/// Resolution is reflective and therefore cached per type — the writer sits on every command
/// that publishes, and property lookups do not belong there.
/// </para>
/// </summary>
public sealed class ConventionOutboxPartitionKeyStrategy : IOutboxPartitionKeyStrategy
{
    private const string EventSuffix = "IntegrationEvent";

    /// <summary>
    /// Tried in order before the subject-noun convention. <c>AggregateId</c> is the explicit
    /// opt-in for a contract that wants to name its own partition without an attribute.
    /// </summary>
    private static readonly string[] ExplicitProperties = ["PartitionKey", "AggregateId"];

    private static readonly ConcurrentDictionary<Type, PropertyInfo?> Cache = new();

    public string? Resolve(IIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var type = integrationEvent.GetType();
        var property = Cache.GetOrAdd(type, FindSubjectProperty);

        if (property is null)
        {
            return null;
        }

        var value = property.GetValue(integrationEvent);

        // A null subject identifier is not an error — it is an event about nothing in
        // particular, and it belongs in the fallback partition rather than one named "".
        var text = value?.ToString();

        return string.IsNullOrWhiteSpace(text) ? null : $"{property.Name}:{text}";
    }

    private static PropertyInfo? FindSubjectProperty(Type type)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var candidate in ExplicitProperties)
        {
            var explicitMatch = properties.FirstOrDefault(property =>
                string.Equals(property.Name, candidate, StringComparison.Ordinal));

            if (explicitMatch is not null)
            {
                return explicitMatch;
            }
        }

        // ProviderPublishedIntegrationEvent → "ProviderPublished" → try "ProviderPublishedId",
        // then "ProviderPublishe...", walking back one word at a time to "ProviderId".
        var name = type.Name;

        if (name.EndsWith(EventSuffix, StringComparison.Ordinal))
        {
            name = name[..^EventSuffix.Length];
        }

        foreach (var noun in SubjectNouns(name))
        {
            var match = properties.FirstOrDefault(property =>
                string.Equals(property.Name, $"{noun}Id", StringComparison.Ordinal));

            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    /// <summary>
    /// Yields the candidate subject nouns of a PascalCase event name, longest first.
    /// <para>
    /// "ProviderRecommendationReceived" yields "ProviderRecommendation", then "Provider" — the
    /// trailing verb is dropped, since no contract carries a "…ReceivedId". So a contract
    /// carrying <c>ProviderRecommendationId</c> partitions on the recommendation, and one
    /// carrying only
    /// <c>ProviderId</c> partitions on the provider. Longest first because the more specific
    /// subject is the more specific ordering guarantee, and a narrower partition is always the
    /// safer default: it orders what must be ordered and blocks less when something fails.
    /// </para>
    /// </summary>
    private static IEnumerable<string> SubjectNouns(string eventName)
    {
        var boundaries = new List<int> { 0 };

        for (var index = 1; index < eventName.Length; index++)
        {
            if (char.IsUpper(eventName[index]))
            {
                boundaries.Add(index);
            }
        }

        for (var index = boundaries.Count - 1; index >= 1; index--)
        {
            yield return eventName[..boundaries[index]];
        }
    }
}
