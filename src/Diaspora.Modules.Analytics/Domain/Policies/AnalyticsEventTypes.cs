namespace Diaspora.Modules.Analytics.Domain.Policies;

/// <summary>
/// Every analytics event name this platform knows, as constants.
/// <para>
/// <b>Closed, and the closure is the privacy design.</b> §40 of the phase brief treats ingestion as
/// untrusted input; the enforcement is that an event name arriving from a client is looked up in
/// the schema registry — seeded from this list — and refused with a reason if it is not there.
/// "Reject visibly rather than store quietly" is FRONTEND-PHASE15-GAP-029's own wording, and a
/// registry that accepted anything would make the client's four-phase-old sanitiser advisory.
/// </para>
/// <para>
/// <b>Names are <c>domain.thing.measure</c>, matching the client's event taxonomy and the metric
/// catalogue</b>, so an event and the metric it feeds sort together in a log and in a table.
/// </para>
/// </summary>
public static class AnalyticsEventTypes
{
    // ── Client-submittable ──────────────────────────────────────────────────────────────────
    //
    // Three, and the shortness of the list is the point. A client may report that somebody looked
    // at something; it may not report that somebody saved, shared, commented, applied, attended or
    // paid, because every one of those is a state change the owning module already commits and
    // publishes. §41: business metrics originate from trusted domain events.

    /// <summary>
    /// A member opened a piece of content.
    /// <para>
    /// Deduplicated to one row per member per item per day, which is not an implementation
    /// convenience — it is the published definition: "People who opened something you published.
    /// One person counted once per item per day."
    /// </para>
    /// </summary>
    public const string ContentViewed = "content.viewed";

    /// <summary>
    /// A member opened a creator's public profile.
    /// <para>
    /// Carries the creator as <c>contentId</c> with <c>contentType=Creator</c> rather than as a
    /// <c>creatorId</c> property, because the client's allow-list bans keys that name a person and
    /// generic content identifiers are exactly what it permits instead.
    /// </para>
    /// </summary>
    public const string ProfileViewed = "profile.viewed";

    /// <summary>
    /// A member saw content in a feed, a rail or a search result without opening it.
    /// <para>
    /// <b>Registered and not yet aggregated into any served metric.</b> Reach is a distinct count
    /// of impressions and this deployment publishes no reach figure, so accepting the event and
    /// serving nothing from it would be the more honest of two wrong answers. It is here so a
    /// client that starts emitting is not refused, and so the day reach is computed the history is
    /// already there rather than starting from zero. See <c>AnalyticsMetricCatalog</c> for why
    /// <c>reach</c> is served as unavailable rather than approximated from views.
    /// </para>
    /// </summary>
    public const string ContentImpression = "content.impression";

    // ── Server-produced ─────────────────────────────────────────────────────────────────────
    //
    // Each is written by a consumer of a published contract. A client submitting one of these is
    // refused with analytics.ingest.event-not-submittable — the registry says which class an event
    // belongs to and the ingestion handler reads it.

    /// <summary>Content became readable by somebody other than its owner.</summary>
    public const string ContentPublished = "content.published";

    /// <summary>Somebody saved a piece of content to come back to.</summary>
    public const string ContentSaved = "content.saved";

    /// <summary>Somebody shared a piece of content.</summary>
    public const string ContentShared = "content.shared";

    /// <summary>Somebody commented on a piece of content.</summary>
    public const string ContentCommented = "content.commented";

    /// <summary>Somebody reacted to a piece of content. The reaction key travels with it.</summary>
    public const string ContentReacted = "content.reacted";

    /// <summary>
    /// Somebody took the action the content asked for.
    /// <para>
    /// One event across five domains, and the domain decides what "the action" is: an RSVP for an
    /// event, an application for a job, a purchase for a marketplace listing, a viewing for a
    /// housing listing. The definition served on the metric names all four, because a conversion
    /// figure whose meaning changes per row and does not say so is worse than no figure.
    /// </para>
    /// </summary>
    public const string ContentConverted = "content.converted";

    /// <summary>Somebody started following a member, an organization or a provider.</summary>
    public const string AudienceFollowed = "audience.followed";

    /// <summary>Somebody stopped following.</summary>
    public const string AudienceUnfollowed = "audience.unfollowed";

    // ── Automation — B24 · FRONTEND-PHASE18-GAP-028 ─────────────────────────────────────────────
    //
    // The names are the client's, exactly. `automation/events.ts` has emitted them since Phase 18
    // through the same buffer every other vocabulary uses, and until this wave every one of them was
    // refused with `analytics.ingest.event-unknown` — a visible refusal, which is the designed
    // behaviour for an unregistered name and was nevertheless the whole of the gap.
    //
    // **Eight are server-produced and four are client-submittable, and the split is not arbitrary.**
    // Whether an automation was created, switched on, paused, switched off, removed or ran is a fact
    // this platform commits and publishes; §41 forbids a client incrementing any of them. What a
    // client alone can report is what happened on the device and produced no server state: opening a
    // builder, abandoning one, testing a rule without running it, and meeting a gap.
    //
    // **Fourteen of the client's twenty-six names are deliberately not here.** Surface views
    // (`center.viewed`, `templates.viewed`, `history.viewed`, `run.viewed`), builder micro-steps
    // (`trigger.selected`, `condition.added`, `action.added`, `preview.viewed`), the saved-search
    // trio, `resumed`, `updated` and `dependency.blocked`. Each would be a registry row, a schema
    // version and a retention obligation bought for a figure nobody has asked for, and §14's rule is
    // to reuse a signal rather than mint one. They continue to be refused visibly, per event, which
    // tells a client release immediately rather than a month later.

    /// <summary>An automation was created. Carries the trigger key and the template it came from.</summary>
    public const string AutomationCreated = "automation.created";

    /// <summary>An automation was switched on. The denominator every execution figure is read against.</summary>
    public const string AutomationActivated = "automation.activated";

    /// <summary>An automation was paused — "stop for now", schedule kept.</summary>
    public const string AutomationPaused = "automation.paused";

    /// <summary>An automation was switched off — "stop, and stop counting".</summary>
    public const string AutomationDisabled = "automation.disabled";

    /// <summary>An automation was removed.</summary>
    public const string AutomationDeleted = "automation.deleted";

    /// <summary>
    /// An automation ran and did not fail.
    /// <para>
    /// One fact per run, chosen against <see cref="AutomationFailed"/> from the run's own terminal
    /// status. <c>Skipped</c> lands here and not there: the trigger fired and a condition said no,
    /// which is the commonest outcome in a healthy platform and is not a failure. The status travels
    /// as the qualifier so a dataset can separate them without a second event type.
    /// </para>
    /// </summary>
    public const string AutomationExecuted = "automation.executed";

    /// <summary>An automation ran and failed. The failure kind travels as the qualifier.</summary>
    public const string AutomationFailed = "automation.failed";

    /// <summary>
    /// An automation was built from a catalogue template.
    /// <para>
    /// Emitted beside <see cref="AutomationCreated"/> rather than instead of it, exactly as the
    /// client does, because "how many automations exist" and "how many came from templates" are
    /// different questions and deriving one from the other would need the template id on every row.
    /// </para>
    /// </summary>
    public const string AutomationTemplateUsed = "automation.template.used";

    /// <summary>
    /// Somebody ran a rule as a dry run.
    /// <para>
    /// <b>Client-attested, and that is honest rather than lazy.</b> A simulation performs nothing and
    /// is never persisted, so there is no server fact to derive it from; a server contract published
    /// for a dry run would be a durable record of a member testing something they then discarded.
    /// The trust class on the row says where it came from, which is what that column is for.
    /// </para>
    /// </summary>
    public const string AutomationTested = "automation.tested";

    /// <summary>Somebody opened the automation builder.</summary>
    public const string AutomationBuilderStarted = "automation.builder.started";

    /// <summary>
    /// Somebody opened the builder and left without saving.
    /// <para>
    /// The one number a builder is judged by, and the one no server record can produce: a member who
    /// abandons a draft leaves nothing behind to count.
    /// </para>
    /// </summary>
    public const string AutomationBuilderAbandoned = "automation.builder.abandoned";

    /// <summary>
    /// A member met a capability this platform does not have.
    /// <para>
    /// The register calls this "the most operationally useful event in this vocabulary", and the
    /// reason is worth restating: it turns the backend gap register from a document into a ranked
    /// list of what members are actually blocked by, which is a better prioritisation input than
    /// anybody's estimate of importance.
    /// </para>
    /// </summary>
    public const string AutomationGapEncountered = "automation.gap.encountered";
}
