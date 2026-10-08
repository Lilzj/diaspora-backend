namespace Diaspora.Modules.Notifications.Infrastructure.DeepLinks;

/// <summary>
/// The client routes this platform knows how to open.
/// <para>
/// <b>Taken from the mobile client's own router, and that is the only reason it can be trusted.</b>
/// Every prefix here corresponds to a screen that exists in the React Native app today. A route that
/// is not on this list either has not shipped yet or is a mistake, and both should cost the link
/// rather than send a member to a blank screen.
/// </para>
/// <para>
/// Prefixes rather than exact routes, because the identifiers are substituted at render time —
/// <c>/job/</c> matches <c>/job/8f2c…</c>. The bare forms are listed separately so a link to a list
/// screen is as valid as one to a record.
/// </para>
/// <para>
/// <b>Remediation Wave G — ten of these were wrong, and the drift was invisible.</b> This table had
/// said <c>/jobs</c>, <c>/guides</c>, <c>/providers</c>, <c>/events</c>, <c>/organizations</c> and
/// <c>/trust</c>; the client's router serves <c>/job/[id]</c>, <c>/guide/[id]</c>,
/// <c>/provider/[id]</c>, <c>/community-life/event/[id]</c>, <c>/community-life/org/[id]</c> and
/// <c>/profile/trust-center</c>. It also omitted <c>/messages</c>, <c>/knowledge</c> and
/// <c>/search</c>, which the client has always had.
/// </para>
/// <para>
/// The consequence was silent on both sides. This resolver refused nothing — the routes passed its
/// own check — and the client's <c>isResolvableRoute</c> then refused them on arrival and fell back
/// to the inbox, which is correct degradation and therefore looks like nothing being wrong. Roughly
/// half of every notification template on the platform deep-linked to a prefix the client would not
/// open, including the highest-volume ones: <c>jobs.application.new</c>,
/// <c>jobs.interview.scheduled</c>, <c>events.reminder</c> and <c>provider.review.received</c>.
/// </para>
/// <para>
/// The plural forms were corrected here rather than in the client because the platform was already
/// internally inconsistent about them: <c>DefaultFeedSectionContributor</c> has always emitted
/// <c>/job</c>, <c>/provider</c> and <c>/community-life/org</c> for its "see all" links. The client's
/// forms are the ones backed by real screens, so they win.
/// </para>
/// <para>
/// A constant rather than configuration: this is the contract between two repositories, and a
/// deployment silently changing it would break the links rather than fix them. A client whose routes
/// genuinely differ registers its own <c>IDeepLinkResolver</c>.
/// </para>
/// <para>
/// <b>This list is asserted against the client's own by a test in each repository.</b> See
/// <c>DeepLinkRouteTableTests</c> here and <c>notification-deep-links.test.ts</c> there. Neither
/// test can see the other repository, so each pins the same literal set — which is what makes a
/// one-sided edit fail rather than drift.
/// </para>
/// </summary>
public static class DeepLinkRouteTable
{
    /// <summary>Route prefixes the mobile client can open. Ordinal comparison throughout.</summary>
    public static readonly IReadOnlyList<string> KnownPrefixes =
    [
        // Shell and account
        "/notifications",
        "/settings",
        "/profile",
        "/verification",
        "/saved",
        "/search",
        "/explore",

        // Discovery domains. Singular where the client's route is singular — these
        // are `[id]` routes, not collections.
        "/job",
        "/employer",
        "/marketplace",
        "/housing",
        "/provider",
        "/guide",
        "/knowledge",
        "/neighborhood",
        "/country",
        // Official notices, embassies and visa advisories (Phase 10). Added 8 Oct 2026 so a
        // notice notification opens the notice rather than falling back to the inbox.
        "/intelligence",
        "/update",
        "/contributor",

        // Community. `/community` is Phase 13's diaspora communities;
        // `/community-life` is Phase 12's organizations and events. Two different
        // domains sharing a word, and both have their own tree.
        "/community",
        "/community-life",

        // Messaging. `/messages` is the tab, `/conversation` a single thread.
        "/messages",
        "/conversation",

        // Money and creator surfaces.
        "/wallet",
        "/monetization",
        "/creator",

        // Automation and moderation.
        "/automation",
        "/moderation"
    ];
}
