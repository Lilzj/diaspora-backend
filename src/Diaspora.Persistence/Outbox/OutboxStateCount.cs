using Diaspora.Platform.Messaging.Outbox.Model;

namespace Diaspora.Persistence.Outbox;

/// <summary>
/// One row of the grouped state count.
/// <para>
/// A named projection rather than an anonymous type so the result can be handed between
/// methods without reflection. The alternative is three separate <c>COUNT</c> queries over the
/// hottest table in the platform, on a timer, on every instance.
/// </para>
/// </summary>
internal sealed record OutboxStateCount(OutboxState State, int Count);
