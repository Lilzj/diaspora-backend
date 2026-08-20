namespace Diaspora.Modules.Guides.Domain.Events;

/// <summary>
/// The one-word verbs the Knowledge Directory's change events carry.
/// <para>
/// <b>A string on the event rather than an event type per verb, and the trade is deliberate.</b>
/// Consumers of the directory almost all want the same thing — this country changed, drop your
/// cache — and giving them five event types to subscribe to in order to maintain one flag is the
/// mistake B8's featured event already avoided by carrying a boolean. The verb is here for the
/// minority of consumers that genuinely care which kind of change it was, and a republished
/// directory really is different from a rearranged one.
/// </para>
/// <para>
/// Constants rather than an enum because the value crosses the wire. An enum would serialize as a
/// number a consumer has to own a copy of the ordering to read, or as a name that changes when
/// somebody renames a member; a lower-case verb is legible in a log and stable under refactoring.
/// </para>
/// </summary>
public static class KnowledgeChangeKinds
{
    public const string Created = "created";

    /// <summary>Names, descriptions, icons, localizations — everything that is not the ordering.</summary>
    public const string Metadata = "metadata";

    public const string Updated = "updated";

    /// <summary>The ordering changed: entries reordered, added or removed.</summary>
    public const string Arranged = "arranged";

    public const string Published = "published";

    public const string Archived = "archived";

    public const string Restored = "restored";
}
