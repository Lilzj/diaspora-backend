using System.Collections.Frozen;

namespace Diaspora.Modules.Automation.Domain.Catalog;

/// <summary>
/// What a trigger's subject <i>is</i>, and the three things that follow from it.
/// <para>
/// <b>Introduced in Wave 12 because a hard-coded literal stopped being harmless.</b> Wave 11's
/// dispatcher wrote every reminder with the subject kind <c>event</c> and the deep link
/// <c>/community-life/event/{id}</c>. That was invisibly correct while a reminder could only ever be
/// about a community event. Wave 12 adds four more things a member can be reminded about — a job
/// closing, a listing expiring, a reservation ending, a viewing appointment — and the literal becomes
/// two defects at once: the deduplication key is
/// <c>(member, subjectKind, subjectId, offset, anchor)</c>, so two subjects of different kinds that
/// share an identifier collide and the second is suppressed as a duplicate of the first; and every
/// reminder deep-links into Community Life whatever it was about.
/// </para>
/// <para>
/// <b>A closed set, and unknown normalises rather than throws.</b> The subject type on a trigger
/// context is written by a consumer, and a consumer added later must not be able to make a member's
/// reminder throw inside a background sweep. An unrecognised kind becomes <see cref="Generic"/>,
/// which deduplicates correctly against itself and carries no deep link — the honest answer, and one
/// that degrades to "the reminder arrives without a link" rather than "the reminder is lost".
/// </para>
/// </summary>
public static class AutomationSubjectKinds
{
    public const string Event = "event";
    public const string Job = "job";
    public const string Listing = "listing";
    public const string Housing = "housing";
    public const string Reservation = "reservation";
    public const string Viewing = "viewing";
    public const string Provider = "provider";
    public const string Guide = "guide";
    public const string Conversation = "conversation";
    public const string Membership = "membership";
    public const string Organization = "organization";
    public const string Application = "application";
    public const string Post = "post";
    public const string Announcement = "announcement";
    public const string Payment = "payment";
    public const string Creator = "creator";

    /// <summary>The answer for a subject kind nothing below recognises.</summary>
    public const string Generic = "subject";

    /// <summary>
    /// Where the client opens this subject, and null where there is nowhere to send them.
    /// <para>
    /// Paths carry the identifier rather than a slug, because a slug is the publishing module's and
    /// is not on a trigger context. Every one of these resolves on the client through the same
    /// identifier-or-slug route the deep-link handler already accepts.
    /// </para>
    /// </summary>
    private static readonly FrozenDictionary<string, string?> Paths =
        new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            // Remediation Wave G. Six of these named a prefix the client's router
            // has no screen for — `/jobs`, `/providers`, `/guides` and
            // `/organizations` are the platform's *API* prefixes, not the app's
            // routes. Every automation notification about a job, provider, guide or
            // organization deep-linked somewhere the client refused to open.
            [Event] = "/community-life/event/",
            [Job] = "/job/",
            [Listing] = "/marketplace/",
            [Housing] = "/housing/",
            [Reservation] = "/marketplace/reservation/",
            [Viewing] = "/housing/viewing/",
            [Provider] = "/provider/",
            [Guide] = "/guide/",
            [Conversation] = "/conversation/",
            [Membership] = "/community-life/membership/",
            [Organization] = "/community-life/org/",
            [Application] = "/job/applications/",
            [Post] = "/community/post/",
            [Announcement] = "/community/announcement/",
            [Payment] = "/wallet/",
            [Creator] = "/creator/",
            [Generic] = null
        }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// What a reminder about this kind is called when the trigger carried no title.
    /// <para>
    /// Read by a member in a notification, so it is a noun phrase rather than a key.
    /// </para>
    /// </summary>
    private static readonly FrozenDictionary<string, string> Titles =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Event] = "An event",
            [Job] = "A job",
            [Listing] = "A listing",
            [Housing] = "A property",
            [Reservation] = "A reservation",
            [Viewing] = "A viewing",
            [Provider] = "A provider",
            [Guide] = "A guide",
            [Conversation] = "A conversation",
            [Membership] = "A membership",
            [Organization] = "An organisation",
            [Application] = "An application",
            [Post] = "A post",
            [Announcement] = "An announcement",
            [Payment] = "A payment",
            [Creator] = "Your creator profile",
            [Generic] = "Something you asked to be reminded about"
        }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>A recognised kind, lower-cased, or <see cref="Generic"/>.</summary>
    public static string Normalise(string? subjectType)
    {
        if (string.IsNullOrWhiteSpace(subjectType))
        {
            return Generic;
        }

        var candidate = subjectType.Trim().ToLowerInvariant();
        return Paths.ContainsKey(candidate) ? candidate : Generic;
    }

    /// <summary>Where to open it, or null.</summary>
    public static string? DeepLink(string subjectKind, Guid subjectId) =>
        Paths.TryGetValue(subjectKind, out var path) && path is not null
            ? $"{path}{subjectId:D}"
            : null;

    public static string FallbackTitle(string subjectKind) =>
        Titles.TryGetValue(subjectKind, out var title) ? title : Titles[Generic];

    /// <summary>
    /// How a due reminder describes the moment it was too late for.
    /// <para>
    /// Wave 11's sweeper said "the event had already started" for every subject. A member reminded
    /// about a job closing does not have an event, and being told they do is the platform describing
    /// something that did not happen.
    /// </para>
    /// </summary>
    public static string LateReason(string subjectKind) => subjectKind switch
    {
        Event => "The event had already started, so this reminder was not sent.",
        Job => "The job had already closed, so this reminder was not sent.",
        Listing or Housing => "The listing had already expired, so this reminder was not sent.",
        Reservation => "The reservation had already ended, so this reminder was not sent.",
        Viewing => "The viewing time had already passed, so this reminder was not sent.",
        Membership => "The renewal date had already passed, so this reminder was not sent.",
        _ => "The moment this was about had already passed, so this reminder was not sent."
    };
}
