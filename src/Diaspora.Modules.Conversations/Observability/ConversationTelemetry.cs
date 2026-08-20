using System.Diagnostics;
using System.Diagnostics.Metrics;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Conversations.Observability;

/// <summary>
/// What this module measures.
/// <para>
/// <b>Counts, never content.</b> There is no metric, span attribute or log field in this module
/// carrying a message body, a participant's name or an entity title. A conversation is the most
/// private surface the platform has, and a dimension that looked harmless — the context title, say —
/// would put "2-bed flat, Peckham" into a metrics backend that has none of this platform's access
/// rules.
/// </para>
/// </summary>
public static class ConversationTelemetry
{
    /// <summary>
    /// The one declaration of the source name in this assembly.
    /// <para>
    /// See the note in <c>ConversationsModuleMetadata</c> for why there is not a second one at the
    /// root namespace: a duplicate type name there wins every resolution by proximity, silently.
    /// </para>
    /// </summary>
    public const string SourceName = ConversationTelemetryNames.Source;

    public static readonly ActivitySource Activity = new(SourceName);

    private static readonly Meter Meter = new(SourceName);

    /// <summary>Conversations opened. Tagged with the context type key, which is reference data.</summary>
    public static readonly Counter<long> ConversationsOpened =
        Meter.CreateCounter<long>("diaspora.conversations.opened");

    /// <summary>Find-or-create that found. A high ratio means the client's dedup key is working.</summary>
    public static readonly Counter<long> ConversationsReused =
        Meter.CreateCounter<long>("diaspora.conversations.reused");

    public static readonly Counter<long> MessagesSent =
        Meter.CreateCounter<long>("diaspora.conversations.messages.sent");

    public static readonly Counter<long> MessagesEdited =
        Meter.CreateCounter<long>("diaspora.conversations.messages.edited");

    public static readonly Counter<long> MessagesDeleted =
        Meter.CreateCounter<long>("diaspora.conversations.messages.deleted");

    /// <summary>
    /// Sends refused because the client replayed a <c>localId</c>.
    /// <para>
    /// Worth its own counter: a rising number is a client retrying too eagerly, which looks like
    /// nothing at all in an error rate because every one of these succeeds.
    /// </para>
    /// </summary>
    public static readonly Counter<long> MessagesDeduplicated =
        Meter.CreateCounter<long>("diaspora.conversations.messages.deduplicated");

    public static readonly Counter<long> Blocked =
        Meter.CreateCounter<long>("diaspora.conversations.blocked");

    /// <summary>Writes refused because a block stands. The number that says blocking works.</summary>
    public static readonly Counter<long> WritesRefusedByBlock =
        Meter.CreateCounter<long>("diaspora.conversations.refused.block");

    /// <summary>Live fan-outs attempted, tagged by whether a socket was there.</summary>
    public static readonly Counter<long> RealtimeBroadcasts =
        Meter.CreateCounter<long>("diaspora.conversations.realtime.broadcasts");

    /// <summary>Notifications suppressed, tagged with which rule suppressed them.</summary>
    public static readonly Counter<long> NotificationsSuppressed =
        Meter.CreateCounter<long>("diaspora.conversations.notifications.suppressed");
}

/// <summary>
/// The names, separate from the instruments so the contributor can be a plain class.
/// </summary>
public static class ConversationTelemetryNames
{
    public const string Source = "Diaspora.Conversations";
}

public sealed class ConversationObservabilityContributor : IObservabilityContributor
{
    public string Module => ConversationsModuleMetadata.Name;

    public IEnumerable<string> ActivitySources => [ConversationTelemetryNames.Source];

    public IEnumerable<string> Meters => [ConversationTelemetryNames.Source];
}
