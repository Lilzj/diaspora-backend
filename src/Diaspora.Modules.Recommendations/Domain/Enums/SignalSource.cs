namespace Diaspora.Modules.Recommendations.Domain.Enums;

/// <summary>
/// Which module's published contract a signal was derived from.
/// <para>
/// <b>Recorded for provenance, never for behaviour.</b> No ranking strategy branches on the source,
/// and none should: a member who viewed a job is equally interested whether the view arrived from
/// the Jobs module or from the client's own instrumentation. The field exists so an operator asking
/// "why does this member's profile think they care about housing" can answer it, and so a defective
/// consumer's signals can be deleted without touching anybody else's.
/// </para>
/// <para>
/// <see cref="Client"/> is the one that is not a module: signals recorded directly by the mobile app
/// through the public endpoint. They are the least trustworthy — a client can claim anything — which
/// is why the recording command clamps their weight and why they carry the caller's identity rather
/// than one supplied in the payload.
/// </para>
/// </summary>
public enum SignalSource
{
    Client = 0,
    Identity = 1,
    Trust = 2,
    Location = 3,
    Media = 4,
    Community = 5,
    Providers = 6,
    Guides = 7,
    Knowledge = 8,
    Jobs = 9,
    Marketplace = 10,
    Housing = 11,
    CommunityLife = 12,
    Notifications = 13,

    /// <summary>Produced by this module about itself — an acceptance, a dismissal.</summary>
    Recommendations = 14,

    /// <summary>
    /// The Financial Platform. A purchased placement, and nothing else.
    /// <para>
    /// Appended rather than inserted: the values are persisted and a renumbering would silently
    /// reattribute every signal already recorded.
    /// </para>
    /// </summary>
    Financial = 15,

    /// <summary>
    /// The Conversations Platform. Somebody opened a thread about something.
    /// <para>
    /// Appended, for the reason on <see cref="Financial"/>. B13.2's own event catalogue names this
    /// module as the consumer of four of its contracts and none was built — the same shape PSR-1.1
    /// found between Financial and its two universal consumers.
    /// </para>
    /// </summary>
    Conversations = 16
}
