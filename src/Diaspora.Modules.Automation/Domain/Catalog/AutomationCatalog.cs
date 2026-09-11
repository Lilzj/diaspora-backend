using System.Collections.Frozen;
using Diaspora.Modules.Automation.Domain.Enums;

namespace Diaspora.Modules.Automation.Domain.Catalog;

/// <summary>
/// A trigger this platform knows about.
/// </summary>
/// <param name="Key">The frontend's key, exactly. <c>event.startingSoon</c>.</param>
/// <param name="Domain">The product area. Drives grouping in the builder.</param>
/// <param name="Delivery">How it reaches the engine.</param>
/// <param name="SourceEventName">
/// The published contract's <b>namespace-qualified</b> type name, or null when the trigger is not
/// event-driven.
/// <para>
/// Qualified rather than short, for the reason B13 already records: Jobs and Marketplace both
/// publish an <c>OfferAcceptedIntegrationEvent</c> and Marketplace and Housing both publish a
/// <c>ReservationCancelledIntegrationEvent</c>. A catalogue keyed on the short name would match both.
/// </para>
/// </param>
/// <param name="IsAvailable">
/// Whether this deployment can actually observe it.
/// <para>
/// <b>False is a first-class answer and it is not a defect.</b> Several triggers in the frontend's
/// catalogue name a contract no module publishes — <c>ListingInquiryReceived</c>,
/// <c>HousingApplicationStatusChanged</c>, <c>ProviderLeadReceived</c>, <c>CommunityMemberJoined</c>.
/// They stay in the catalogue so the builder can explain them, and activation is refused with a 400
/// naming the missing contract. The alternative — omitting them — makes a member's saved draft
/// unopenable; the other alternative — accepting them — makes a member switch on a rule that will
/// never fire and conclude the platform is broken.
/// </para>
/// </param>
/// <param name="UnavailableReason">What is missing, in the owning module's own vocabulary.</param>
/// <param name="RequiredCapability">A server-granted capability, re-checked at execution.</param>
/// <param name="AllowedOwners">Owner kinds permitted to use it at all.</param>
/// <param name="RequiresPrivacyConsent">True when it reads something private enough to need consent.</param>
public sealed record TriggerCatalogEntry(
    string Key,
    string Domain,
    AutomationTriggerDelivery Delivery,
    string? SourceEventName,
    bool IsAvailable,
    string? UnavailableReason,
    string? RequiredCapability,
    IReadOnlyList<AutomationOwnerKind> AllowedOwners,
    bool RequiresPrivacyConsent);

/// <summary>
/// A condition this platform knows about.
/// </summary>
/// <param name="RequiresServerEvaluation">
/// §9's instruction as a field: true for anything a member could change locally to gain something,
/// and also for anything whose client copy can be stale in a way that matters.
/// </param>
/// <param name="IsSensitive">
/// Whether the <i>value</i> is personal. Governs logging and analytics rather than evaluation — the
/// two are genuinely independent. A run record never quotes a sensitive condition's value.
/// </param>
/// <param name="IsServerEvaluable">
/// Whether <b>this module</b> can answer it from a trigger payload.
/// <para>
/// False for <c>walletBalance</c>, <c>subscriptionActive</c>, <c>creatorStatus</c>,
/// <c>metricAbove</c> and <c>metricBelow</c>: those are entitlement and analytics figures B15, B18
/// and B19 own, and <b>no trigger payload may answer them</b>. A condition that cannot be evaluated
/// fails <b>closed</b> — the run is skipped and the outcome says which condition could not be
/// answered — because a condition optimistically matched is a member told something untrue.
/// <para>
/// Since Wave 12 those five have an <see cref="ExternalSource"/> instead, and are answered by the
/// module that owns the figure. This flag stays false for them, and that is deliberate: it means
/// "answerable from the trigger payload", and a payload must never be able to satisfy one.
/// </para>
/// </para>
/// </param>
/// <param name="ExternalSource">
/// The owning module that answers this, when nothing in a payload can — Wave 12.
/// <para>
/// Null for every condition the payload carries. Non-null puts the key on the resolver's list: before
/// evaluation the engine asks whichever <c>IAutomationConditionSource</c> declares it, and writes the
/// answer into a dictionary the evaluator consults <i>instead of</i> the trigger's values for this key.
/// </para>
/// <para>
/// <b>With no source registered, the behaviour is exactly Wave 11's</b> — no answer, fail closed, and
/// the run skipped with <see cref="UnavailableReason"/>. That is the honest state for a deployment
/// where the owning module has not claimed the seam, and it is why the reason string stays on the
/// entry rather than being deleted when the port arrives.
/// </para>
/// </param>
public sealed record ConditionCatalogEntry(
    string Key,
    string Domain,
    string ValueKind,
    IReadOnlyList<string> Operators,
    bool RequiresServerEvaluation,
    bool IsSensitive,
    bool IsServerEvaluable,
    string? UnavailableReason,
    string? ExternalSource = null);

/// <summary>
/// An action this platform knows about.
/// </summary>
/// <param name="NotificationCategoryKey">
/// The B13 category an action's message belongs to. Preferences apply.
/// <para>
/// The action names a category; it does not name a channel, a priority or a template. Those are
/// B13's routing rules, which are reference data. An automation that could name its own channel
/// would be an automation that could route around a member's preferences.
/// </para>
/// </param>
/// <param name="ReachesOtherPeople">
/// Drives the audience preview, the capability check and the stronger confirmation. Every action
/// where this is true has a non-null <paramref name="RequiredCapability"/>, and the invariant is
/// asserted rather than reviewed.
/// </param>
public sealed record ActionCatalogEntry(
    string Key,
    string Domain,
    AutomationActionExecutor Executor,
    AutomationRepeatRisk RepeatRisk,
    string? NotificationCategoryKey,
    string? RequiredCapability,
    bool ReachesOtherPeople,
    bool IsAvailable,
    string? UnavailableReason,
    IReadOnlyList<AutomationOwnerKind> AllowedOwners);

/// <summary>
/// The B13 category each notifying action's message belongs to.
/// <para>
/// Declared here rather than imported from the module's notification catalogue, so the domain
/// vocabulary does not take a dependency on the Communication Platform's assembly. They are strings
/// naming a category B13 owns; the contributor that seeds them reads these, not the other way round.
/// </para>
/// </summary>
public static class AutomationCategoryKeys
{
    /// <summary>Notifications an automation sends to its own owner.</summary>
    public const string Personal = "automation.personal";

    /// <summary>Reminders. Separate because a member mutes an alert and keeps a reminder.</summary>
    public const string Reminder = "automation.reminder";

    /// <summary>A community-scoped broadcast. B13's own community announcement category.</summary>
    public const string Community = "community.announcement";

    public const string Organization = "organization.announcement";

    public const string Creator = "creator.update";
}

/// <summary>
/// The vocabulary an automation may be written in, and the only one.
/// <para>
/// <b>This is the allow-list, and it is the whole of the "no arbitrary code" guarantee.</b> A
/// condition is a key from this catalogue, an operator from that key's list and a scalar value. An
/// action is a key from this catalogue and a bag of scalars. There is no expression, no script, no
/// query, no template language and no URL — not because the evaluator sanitises them but because
/// there is nowhere to put one. A client that invents a key receives 400 on write and, if a stored
/// rule ever names one, the engine fails it closed at execution.
/// </para>
/// <para>
/// <b>Every key here is the frontend's key, character for character.</b> They were taken from
/// <c>lib/automation/triggers.ts</c>, <c>conditions.ts</c> and <c>actions.ts</c> by way of the three
/// generated catalogues, and a contract test asserts the three sets have not drifted.
/// </para>
/// <para>
/// <b>No government trigger appears below.</b> Wave 10 / B22.x Government Data is paused pending a
/// separate product-scope decision, and the honest way to hold that is for the vocabulary to not
/// contain the words — an entry marked unavailable would still be a promise that a feed exists
/// behind it. See the final report's deferral record.
/// </para>
/// </summary>
public static class AutomationCatalog
{
    private static readonly AutomationOwnerKind[] EveryBusinessOwner =
    [
        AutomationOwnerKind.User,
        AutomationOwnerKind.Creator,
        AutomationOwnerKind.Provider,
        AutomationOwnerKind.Employer,
        AutomationOwnerKind.Landlord,
        AutomationOwnerKind.Seller
    ];

    private static readonly AutomationOwnerKind[] UserOnly = [AutomationOwnerKind.User];

    private static readonly string[] Equality = ["equals", "notEquals", "includes"];
    private static readonly string[] Membership = ["equals", "includes", "excludes"];
    private static readonly string[] Numeric = ["greaterThan", "lessThan"];

    /// <summary>
    /// For anything that would have to address a community.
    /// <para>
    /// Not a gap in this module. <c>PublishAnnouncementCommandHandler</c> refuses every announcement
    /// scope but <c>Organization</c> and states why: an announcement needs "an owner that can say
    /// who may address them", and a community has members and no such owner. Quoted here rather
    /// than paraphrased, so the two cannot drift into disagreeing about the same decision.
    /// </para>
    /// </summary>
    private const string NoCommunityAudience =
        "A community has members and nobody who may address them. Diaspora only lets an "
        + "organization send to its own members, so there is no audience for this yet.";

    /// <summary>
    /// For a trigger that needs a contract <i>and</i> an owner kind this deployment grants to nobody.
    /// <para>
    /// Separate from <c>NoContract</c> on purpose. A reason naming only the missing contract invites
    /// somebody to publish it and discover the trigger is still unreachable, which is the more
    /// expensive half of the work wasted.
    /// </para>
    /// </summary>
    private const string NoContractAndNoCommunityOwner =
        "Two things are missing. The owning module does not publish a contract for this yet, and "
        + "nobody administers a community on this platform — a community has members and no roles, "
        + "so there is no one an automation about it could belong to.";

    /// <summary>The reason string used wherever an owning module has not published the contract.</summary>
    private const string NoContract =
        "The owning module does not publish a contract for this yet, so the platform cannot observe it.";

    // ── Triggers ────────────────────────────────────────────────────────────────────────────────

    public static readonly FrozenDictionary<string, TriggerCatalogEntry> Triggers =
        new TriggerCatalogEntry[]
        {
            new("manual.run", "notifications", AutomationTriggerDelivery.Manual,
                null, true, null, null, EveryBusinessOwner, false),

            // ── Events — B12/B12.1 publish five of the six ──────────────────────────────────
            new("event.created", "events", AutomationTriggerDelivery.DomainEvent,
                "Diaspora.Contracts.CommunityLife.V1.Events.EventCreatedIntegrationEvent",
                true, null, null, UserOnly, false),
            new("event.updated", "events", AutomationTriggerDelivery.DomainEvent,
                "Diaspora.Contracts.CommunityLife.V1.Events.EventUpdatedIntegrationEvent",
                true, null, null, UserOnly, false),
            new("event.cancelled", "events", AutomationTriggerDelivery.DomainEvent,
                "Diaspora.Contracts.CommunityLife.V1.Events.EventCancelledIntegrationEvent",
                true, null, null, UserOnly, false),
            new("event.rsvpConfirmed", "events", AutomationTriggerDelivery.DomainEvent,
                "Diaspora.Contracts.CommunityLife.V1.Events.RsvpSubmittedIntegrationEvent",
                true, null, null, UserOnly, false),
            new("event.occurrenceChanged", "events", AutomationTriggerDelivery.DomainEvent,
                "Diaspora.Contracts.CommunityLife.V1.Events.EventOccurrenceRescheduledIntegrationEvent",
                true, null, null, UserOnly, false),

            // The sixth is a projection over start times rather than anything anybody publishes.
            new("event.startingSoon", "events", AutomationTriggerDelivery.Schedule,
                null, true, null, null, UserOnly, false),

            // ── Saved searches — one trigger family, five publishing modules ────────────────
            new("savedSearch.newMatch", "search", AutomationTriggerDelivery.DomainEvent,
                null, true, null, null, UserOnly, false),
            new("savedSearch.availabilityChanged", "search", AutomationTriggerDelivery.DomainEvent,
                null, true, null, null, UserOnly, false),
            // Wave 12. Marketplace's `ListingUpdated` has carried `ChangedFields`, `PriceAmount` and
            // `PreviousPriceAmount` since B10; Housing's equivalent is Wave 12's own addition. Two
            // publishing modules, so no single contract name belongs on the entry — see
            // `MultiSourceTriggers`.
            new("savedSearch.priceChanged", "search", AutomationTriggerDelivery.DomainEvent,
                null, true, null, null, UserOnly, false),

            // ── Jobs ────────────────────────────────────────────────────────────────────────
            new("job.applicationStatusChanged", "jobs", AutomationTriggerDelivery.DomainEvent,
                "Diaspora.Contracts.Jobs.V1.Events.ApplicationStatusChangedIntegrationEvent",
                true, null, null, UserOnly, false),
            new("job.applicationSubmitted", "jobs", AutomationTriggerDelivery.DomainEvent,
                "Diaspora.Contracts.Jobs.V1.Events.ApplicationSubmittedIntegrationEvent",
                true, null, AutomationCapabilities.Employer,
                [AutomationOwnerKind.Employer], false),
            new("job.applicationThreshold", "jobs", AutomationTriggerDelivery.DomainEvent,
                "Diaspora.Contracts.Jobs.V1.Events.ApplicationSubmittedIntegrationEvent",
                true, null, AutomationCapabilities.Employer,
                [AutomationOwnerKind.Employer], false),
            // Wave 12. `JobPublishedIntegrationEvent.ClosingAt` — "when applications stop" — has been
            // on the wire since B9; Wave 11 recorded it as absent from the frontend register's column
            // rather than from the contract. A horizon row remembers it, and the sweep fans it out.
            new("job.closingSoon", "jobs", AutomationTriggerDelivery.Schedule,
                null, true, null, null, UserOnly, false),

            // ── Marketplace ─────────────────────────────────────────────────────────────────
            new("listing.offerReceived", "marketplace", AutomationTriggerDelivery.DomainEvent,
                "Diaspora.Contracts.Marketplace.V1.Events.OfferCreatedIntegrationEvent",
                true, null, AutomationCapabilities.Seller,
                [AutomationOwnerKind.Seller], false),
            // Wave 12. An enquiry is a conversation, and B13.2 publishes it: the context type on
            // `ConversationCreatedIntegrationEvent` says which listing it is about. No Marketplace,
            // Housing or Providers contract was needed — three parallel enquiry contracts would have
            // been three copies of a fact one module already owns.
            new("listing.inquiryReceived", "marketplace", AutomationTriggerDelivery.DomainEvent,
                "Diaspora.Contracts.Conversations.V1.Events.ConversationCreatedIntegrationEvent",
                true, null, AutomationCapabilities.Seller,
                [AutomationOwnerKind.Seller], false),

            // Wave 12. `ListingPublishedIntegrationEvent.ExpiresAt` and
            // `ListingReservedIntegrationEvent.ExpiresAt` were both already published; a horizon row
            // remembers each, and the sweep fans it out.
            new("listing.expiring", "marketplace", AutomationTriggerDelivery.Schedule,
                null, true, null, AutomationCapabilities.Seller,
                [AutomationOwnerKind.Seller], false),
            new("listing.reservationEnding", "marketplace", AutomationTriggerDelivery.Schedule,
                null, true, null, AutomationCapabilities.Seller,
                [AutomationOwnerKind.Seller], false),

            // ── Housing ─────────────────────────────────────────────────────────────────────
            new("housing.inquiryReceived", "housing", AutomationTriggerDelivery.DomainEvent,
                "Diaspora.Contracts.Conversations.V1.Events.ConversationCreatedIntegrationEvent",
                true, null, AutomationCapabilities.Landlord,
                [AutomationOwnerKind.Landlord], false),

            // Wave 12. Housing raised the domain event on every transition and published only the
            // three that return units, so a seeker could learn a landlord refused and not that one
            // agreed. `HousingApplicationStatusChangedIntegrationEvent` is Wave 12's addition.
            new("housing.applicationStatusChanged", "housing", AutomationTriggerDelivery.DomainEvent,
                "Diaspora.Contracts.Housing.V1.Events.HousingApplicationStatusChangedIntegrationEvent",
                true, null, null, UserOnly, false),

            // Wave 12. `ViewingScheduledIntegrationEvent.ScheduledFor` has been published by
            // Housing's relay since B11 — Wave 11's "Housing publishes no viewing appointment" was
            // read off the frontend register rather than the contracts assembly.
            new("housing.viewingReminder", "housing", AutomationTriggerDelivery.Schedule,
                null, true, null, null, UserOnly, false),

            // ── Community and organizations ─────────────────────────────────────────────────
            // Organization as well as Community, because the consumer already names both.
            // `CommunityAnnouncementPublishedIntegrationEvent` carries a `Scope` that may be
            // `Organization`, and `CommunityAnnouncementAutomationConsumer` passes
            // `Owners(ScopeId, OrganizationId)` — so an organisation-owned automation on this
            // trigger matches the moment its organisation publishes an announcement.
            //
            // The capability moved for the same reason the owner kind did. **Nothing grants
            // `automations.community`** — the three grants in this platform are
            // `SelfOnlyScopeAuthority.PersonalCapabilities`, CommunityLife's
            // `automations.organization` and Creator's `automations.creator` — so the trigger was
            // gated on a capability no member can hold. That is the same defect one layer down and
            // from the same cause. `automations.organization` is the one an organisation
            // administrator actually holds, and the one `notify.organization` already requires.
            //
            // When a module grants community administration, this needs revisiting: a catalogue
            // entry carries one capability and there would then be two owners with different ones.
            // `AutomationOwnerReachabilityTests` fails the day that happens, which is the reminder.
            //
            // It could not be created. This entry allowed the Community owner kind alone, and no
            // module implements `IAutomationScopeAuthority` for it: `SelfOnlyScopeAuthority` returns
            // `None()` and nothing overrides that, because a community in this platform has members
            // and no administrators — roles exist for organisations only. So a complete capability
            // was served, consumed, matched and unreachable, and the catalogue reported it
            // available the whole time.
            new("community.announcementPublished", "community", AutomationTriggerDelivery.DomainEvent,
                "Diaspora.Contracts.CommunityProgramme.V1.Events.CommunityAnnouncementPublishedIntegrationEvent",
                true, null, AutomationCapabilities.Organization,
                [AutomationOwnerKind.Organization], false),
            new("community.updatePublished", "community", AutomationTriggerDelivery.DomainEvent,
                "Diaspora.Contracts.Community.V1.Events.CommunityPostPublishedIntegrationEvent",
                true, null, null, UserOnly, false),
            // ── Two obstacles each, and the reason strings used to name one ──────────────────
            //
            // `NoContract` is true: nothing publishes a community membership, milestone or event.
            // It is also not the binding one. Every one of these is owned by the Community kind,
            // and **no module grants that kind** — `SelfOnlyScopeAuthority` answers `None()` for it
            // and nothing overrides it, because a community here has members and no administrators.
            // `CommunityMembership` carries a state (Discovering, Interested, Following, Member)
            // and no role; roles are an organisation concept.
            //
            // Publishing the three contracts would leave all three still unreachable. Saying so
            // here is the difference between a gap somebody can close and a gap somebody closes
            // half of and finds nothing changed.
            new("community.memberJoined", "community", AutomationTriggerDelivery.DomainEvent,
                null, false, NoContractAndNoCommunityOwner, AutomationCapabilities.Community,
                [AutomationOwnerKind.Community], false),
            new("community.milestoneReached", "community", AutomationTriggerDelivery.DomainEvent,
                null, false, NoContractAndNoCommunityOwner, AutomationCapabilities.Community,
                [AutomationOwnerKind.Community], false),
            new("community.eventCreated", "community", AutomationTriggerDelivery.DomainEvent,
                null, false, NoContractAndNoCommunityOwner, AutomationCapabilities.Community,
                [AutomationOwnerKind.Community], false),

            new("organization.memberJoined", "organizations", AutomationTriggerDelivery.DomainEvent,
                "Diaspora.Contracts.CommunityLife.V1.Events.OrganizationMemberJoinedIntegrationEvent",
                true, null, AutomationCapabilities.Organization,
                [AutomationOwnerKind.Organization], false),
            new("organization.membershipRenewalDue", "organizations", AutomationTriggerDelivery.DomainEvent,
                "Diaspora.Contracts.CommunityProgramme.V1.Events.ProgrammeMembershipStatusChangedIntegrationEvent",
                true, null, AutomationCapabilities.Organization,
                [AutomationOwnerKind.Organization], false),
            // Wave 12. "Someone registers for our event" read from the organization's side. The RSVP
            // contract has carried `OrganizationId` since B12 and the organizer since B13.1, so the
            // consumer that already existed yields a second context rather than a new contract being
            // published.
            new("organization.registrationReceived", "organizations", AutomationTriggerDelivery.DomainEvent,
                "Diaspora.Contracts.CommunityLife.V1.Events.RsvpSubmittedIntegrationEvent",
                true, null, AutomationCapabilities.Organization,
                [AutomationOwnerKind.Organization], false),

            // ── Guides ──────────────────────────────────────────────────────────────────────
            new("guide.published", "guides", AutomationTriggerDelivery.DomainEvent,
                "Diaspora.Contracts.Guides.V1.Events.GuidePublishedIntegrationEvent",
                true, null, null, UserOnly, false),
            new("guide.savedChanged", "guides", AutomationTriggerDelivery.DomainEvent,
                "Diaspora.Contracts.Guides.V1.Events.GuideUpdatedIntegrationEvent",
                true, null, null, UserOnly, false),
            new("guide.milestoneReached", "guides", AutomationTriggerDelivery.DomainEvent,
                "Diaspora.Contracts.Guides.V1.Events.GuideViewMilestoneReachedIntegrationEvent",
                true, null, null, [AutomationOwnerKind.User, AutomationOwnerKind.Creator], false),

            // ── Creator ─────────────────────────────────────────────────────────────────────
            // ── No capability, because none of these reaches anybody ────────────────────────
            //
            // `CreatorAutomationAuthority` grants `automations.creator` only where the creator may
            // broadcast, and states the rule it is enforcing: *"The bar for owning is lower than the
            // bar for broadcasting, deliberately. Any recognised creator may automate their own
            // affairs - a milestone reaching them, a reward becoming available."*
            //
            // These four notify nobody but the owner - every action they offer is personal - and all
            // four required that capability. So a Contributor, an EmergingCreator or somebody
            // PendingVerification was granted `CanOwn: true` and could select no creator trigger at
            // all, including the two that sentence names by example. The catalogue was enforcing the
            // opposite of the rule the authority had written down.
            //
            // Broadcasting is unaffected and stays gated where it belongs: `notify.followers` keeps
            // `AutomationCapabilities.Creator`, and it is the only creator part that reaches anyone.
            // Ownership is still gated too - the authority refuses a status outside `MayOwn`, and
            // refuses a suspended or restricted creator in the moment before every dispatch.
            new("creator.milestoneReached", "creator", AutomationTriggerDelivery.DomainEvent,
                "Diaspora.Contracts.Creator.V1.Events.CreatorMilestoneCompletedIntegrationEvent",
                true, null, null,
                [AutomationOwnerKind.Creator], false),
            new("creator.referralCompleted", "creator", AutomationTriggerDelivery.DomainEvent,
                "Diaspora.Contracts.Creator.V1.Events.CreatorReferralQualifiedIntegrationEvent",
                true, null, null,
                [AutomationOwnerKind.Creator], false),
            new("creator.rewardAvailable", "creator", AutomationTriggerDelivery.DomainEvent,
                "Diaspora.Contracts.Financial.V1.Events.RewardPointsGrantedIntegrationEvent",
                true, null, null,
                [AutomationOwnerKind.Creator], false),
            // Phase 5. The Backend Gap Build Order listed `ChallengeCompleted` among the four
            // "genuinely missing" events behind the automation triggers. It was not missing.
            // `CreatorChallengeCompletedIntegrationEvent` is published by `CreatorEventHandlers`,
            // once, on the transition to complete - and nothing here consumed it. The client
            // register went further and recorded "no challenge contract at all"; that was written
            // on 11 Sep 2026 without a grep, and was false the day it was written.
            //
            // No capability, for the rule its three siblings follow: a challenge enrollment is "one
            // creator's participation in one challenge", so completing one is the creator's own
            // affair and notifies nobody else.
            new("creator.challengeCompleted", "creator", AutomationTriggerDelivery.DomainEvent,
                "Diaspora.Contracts.Creator.V1.Events.CreatorChallengeCompletedIntegrationEvent",
                true, null, null,
                [AutomationOwnerKind.Creator], false),
            // Wave 12. Answered by the Analytics Platform through `IAutomationConditionSource`, on a
            // schedule the member sets: the trigger is a scheduled automation whose `metricAbove` or
            // `metricBelow` condition is resolved from B19 at run time. Available in the catalogue,
            // and every run of it still fails closed if no deployment registers the source.
            // Same rule, and this is the one it was hiding. Armed by the member's own schedule
            // through the occurrence sweep - the builder attaches a daily one to any
            // `delivery: schedule` trigger - and answered by
            // `AnalyticsAutomationConditionSource`, which fixes the figure at ContentViews over the
            // last thirty days and says why. Every part of it was built. It asked a creator for a
            // capability granted only to somebody allowed to address other people, to be told about
            // their own view count.
            new("creator.performanceThreshold", "analytics", AutomationTriggerDelivery.Schedule,
                null, true, null, null,
                [AutomationOwnerKind.Creator], false),

            // ── Money — notified about, never moved ──────────────────────────────────────────
            new("payment.completed", "wallet", AutomationTriggerDelivery.DomainEvent,
                "Diaspora.Contracts.Financial.V1.Events.PaymentCapturedIntegrationEvent",
                true, null, AutomationCapabilities.Financial, UserOnly, true),
            new("payment.failed", "wallet", AutomationTriggerDelivery.DomainEvent,
                "Diaspora.Contracts.Financial.V1.Events.PaymentFailedIntegrationEvent",
                true, null, AutomationCapabilities.Financial, UserOnly, true),
            new("payment.refunded", "wallet", AutomationTriggerDelivery.DomainEvent,
                "Diaspora.Contracts.Financial.V1.Events.RefundIssuedIntegrationEvent",
                true, null, AutomationCapabilities.Financial, UserOnly, true),
            new("giving.donationCompleted", "wallet", AutomationTriggerDelivery.DomainEvent,
                "Diaspora.Contracts.Financial.V1.Events.DonationCompletedIntegrationEvent",
                true, null, AutomationCapabilities.Financial, UserOnly, true),

            // B15 already publishes both of these as events, so neither needs the scheduler the
            // register assumed. GAP-019 is closed by the module that owns the dates.
            new("subscription.expiring", "monetization", AutomationTriggerDelivery.DomainEvent,
                "Diaspora.Contracts.Financial.V1.Events.SubscriptionExpiringIntegrationEvent",
                true, null, null, EveryBusinessOwner, false),
            new("placement.ending", "monetization", AutomationTriggerDelivery.DomainEvent,
                "Diaspora.Contracts.Financial.V1.Events.PlacementExpiringIntegrationEvent",
                true, null, null,
                [
                    AutomationOwnerKind.Creator, AutomationOwnerKind.Provider,
                    AutomationOwnerKind.Employer, AutomationOwnerKind.Landlord,
                    AutomationOwnerKind.Seller
                ], false),

            // ── Providers ───────────────────────────────────────────────────────────────────
            new("provider.reviewReceived", "providers", AutomationTriggerDelivery.DomainEvent,
                "Diaspora.Contracts.Providers.V1.Events.ProviderReviewReceivedIntegrationEvent",
                true, null, AutomationCapabilities.Provider,
                [AutomationOwnerKind.Provider], false),
            new("provider.featuredEnding", "providers", AutomationTriggerDelivery.DomainEvent,
                "Diaspora.Contracts.Financial.V1.Events.PlacementExpiringIntegrationEvent",
                true, null, AutomationCapabilities.Provider,
                [AutomationOwnerKind.Provider], false),
            // Wave 12. A lead is somebody opening a conversation through a provider's profile, which
            // B13.2 seeds as the `ProviderInquiry` and `BusinessAccount` context types with
            // `OwningModule = "Providers"` on each. `Provider.RecordContact()` exists on the aggregate
            // and is called from nowhere; a route that called it would have been a new product
            // surface invented to satisfy an automation trigger.
            new("provider.leadReceived", "providers", AutomationTriggerDelivery.DomainEvent,
                "Diaspora.Contracts.Conversations.V1.Events.ConversationCreatedIntegrationEvent",
                true, null, AutomationCapabilities.Provider,
                [AutomationOwnerKind.Provider], false),

            // ── Messaging — counts and timestamps, never content ────────────────────────────
            // Wave 12. Conversations now publishes both facts from its own attention sweep, which is
            // exactly what Wave 11 said had to happen: "Conversations owns that and must publish the
            // fact". Delivery is `DomainEvent` rather than `Schedule` because from this module's side
            // they are consumed messages — the schedule is B13.2's, over state this module never sees.
            new("conversation.inactive", "messaging", AutomationTriggerDelivery.DomainEvent,
                "Diaspora.Contracts.Conversations.V1.Events.ConversationInactivityDetectedIntegrationEvent",
                true, null, null, UserOnly, true),
            new("conversation.unreadReminder", "messaging", AutomationTriggerDelivery.DomainEvent,
                "Diaspora.Contracts.Conversations.V1.Events.ConversationUnreadThresholdReachedIntegrationEvent",
                true, null, null, UserOnly, true),

            // ── Location — three modes live on the device, two refused ──────────────────────
            new("location.enteredArea", "location", AutomationTriggerDelivery.DeviceLocal,
                null, false,
                "Background location is not implemented and automations.location is granted to "
                + "nobody. The client refuses this trigger for the same reason.",
                AutomationCapabilities.Location, UserOnly, true),
            new("location.countryChanged", "location", AutomationTriggerDelivery.DeviceLocal,
                null, false,
                "The device observes this while the app is in the foreground. The server never "
                + "claims to.", null, UserOnly, true)
        }.ToFrozenDictionary(trigger => trigger.Key, StringComparer.Ordinal);

    // ── Conditions ──────────────────────────────────────────────────────────────────────────────

    public static readonly FrozenDictionary<string, ConditionCatalogEntry> Conditions =
        new ConditionCatalogEntry[]
        {
            new("country", "community", "country", Equality, false, false, true, null),
            new("community", "community", "community", Membership, true, false, true, null),
            new("neighbourhood", "location", "neighbourhood", ["equals", "includes"], false, true, true, null),
            new("distance", "location", "distance", ["lessThan", "greaterThan", "within"], false, true, true, null),
            new("category", "search", "category", Membership, false, false, true, null),
            new("eventType", "events", "choice", ["equals", "includes"], false, true, true, null),
            new("urgency", "community", "choice", ["equals", "greaterThan"], false, false, true, null),
            new("dayOfWeek", "events", "dayOfWeek", ["includes", "excludes"], false, false, true, null),
            new("timeOfDay", "events", "time", ["between"], false, false, true, null),
            new("priceBelow", "marketplace", "money", ["lessThan"], false, true, true, null),
            new("priceAbove", "marketplace", "money", ["greaterThan"], false, true, true, null),
            new("amountAbove", "wallet", "money", ["greaterThan"], true, true, true, null),
            new("paymentKind", "wallet", "choice", ["equals", "includes"], true, true, true, null),
            new("countAbove", "analytics", "number", ["greaterThan"], true, false, true, null),
            new("ratingBelow", "providers", "number", ["lessThan"], false, false, true, null),
            new("applicationStatus", "jobs", "choice", ["equals", "includes"], true, true, true, null),
            new("listingStatus", "marketplace", "choice", ["equals", "includes"], true, false, true, null),

            // The five no payload may answer. `IsServerEvaluable` stays false — that flag means
            // "answerable from the trigger payload", and a consumer that put a balance in one would
            // let any publishing module decide whether a member's balance condition matched.
            //
            // Wave 12 gives each an owning module instead. The reason strings stay, and are what a
            // member is told when that module has not claimed the seam in this deployment.
            new("walletBalance", "wallet", "money", Numeric, true, true, false,
                "A balance is the Financial Platform's, and nothing in this deployment answers for it.",
                AutomationConditionSources.Entitlement),
            new("subscriptionActive", "monetization", "boolean", ["isTrue", "isFalse"], true, true, false,
                "Entitlement is the Financial Platform's, and nothing in this deployment answers for it.",
                AutomationConditionSources.Entitlement),
            new("creatorStatus", "creator", "choice", ["equals", "greaterThan"], true, false, false,
                "Creator standing is the Creator Platform's, and nothing in this deployment answers for it.",
                AutomationConditionSources.Creator),
            new("metricAbove", "analytics", "number", ["greaterThan"], true, false, false,
                "Analytics figures are the Analytics Platform's, and nothing in this deployment "
                + "answers for them.",
                AutomationConditionSources.Metric),
            new("metricBelow", "analytics", "number", ["lessThan"], true, false, false,
                "Analytics figures are the Analytics Platform's, and nothing in this deployment "
                + "answers for them.",
                AutomationConditionSources.Metric)
        }.ToFrozenDictionary(condition => condition.Key, StringComparer.Ordinal);

    // ── Actions ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The twelve actions, and the three families that are deliberately absent.
    /// <para>
    /// <b>Nothing that moves money.</b> No transfer, no withdrawal, no credit, no refund, no charge,
    /// no renewal, no claim. The enforcement is not a check — it is that no such entry exists, no
    /// amount or destination field exists anywhere in this module's domain model, and an architecture
    /// test asserts the assembly names no financial write.
    /// </para>
    /// <para>
    /// <b>Nothing that sends a message.</b> An auto-reply would need a per-run confirmation design
    /// nobody has asked for, so rather than ship a half-authorized send there is no send.
    /// </para>
    /// <para>
    /// <b>Nothing that posts publicly.</b> An automation that posts on somebody's behalf is a
    /// statement attributed to them that they did not write at the moment it went out, and the
    /// platform has no review-before-publish flow to hang it on.
    /// </para>
    /// </summary>
    public static readonly FrozenDictionary<string, ActionCatalogEntry> Actions =
        new ActionCatalogEntry[]
        {
            new("notify.self", "notifications", AutomationActionExecutor.Server,
                AutomationRepeatRisk.Deduplicated, AutomationCategoryKeys.Personal,
                null, false, true, null, EveryBusinessOwner),

            new("notify.push", "notifications", AutomationActionExecutor.Server,
                AutomationRepeatRisk.Deduplicated, AutomationCategoryKeys.Personal,
                null, false, true, null, EveryBusinessOwner),

            new("notify.email", "notifications", AutomationActionExecutor.Server,
                AutomationRepeatRisk.Deduplicated, AutomationCategoryKeys.Personal,
                null, false, true, null, EveryBusinessOwner),

            new("reminder.schedule", "events", AutomationActionExecutor.Server,
                AutomationRepeatRisk.Deduplicated, AutomationCategoryKeys.Reminder,
                null, false, true, null, EveryBusinessOwner),

            // Unavailable, and not for want of an implementation — `BroadcastAsync` is written and
            // the dispatcher routes to it. It has no audience it could ever resolve.
            // `PublishAnnouncementCommandHandler` refuses every announcement scope but Organization,
            // saying the platform's decision plainly: "Neighbourhood, event and campaign
            // announcements need an owner that can say who may address them." A community is one of
            // those. It has members and nobody who may address them.
            //
            // It was `IsAvailable: true` and allowed only the Community owner kind, which is granted
            // to nobody — so the catalogue advertised an action no member could select and the
            // platform could not have performed.
            new("notify.community", "community", AutomationActionExecutor.Server,
                AutomationRepeatRisk.Deduplicated, AutomationCategoryKeys.Community,
                AutomationCapabilities.Community, true, false, NoCommunityAudience,
                [AutomationOwnerKind.Community]),

            new("notify.organization", "organizations", AutomationActionExecutor.Server,
                AutomationRepeatRisk.Deduplicated, AutomationCategoryKeys.Organization,
                AutomationCapabilities.Organization, true, true, null,
                [AutomationOwnerKind.Organization]),

            new("notify.followers", "creator", AutomationActionExecutor.Server,
                AutomationRepeatRisk.Deduplicated, AutomationCategoryKeys.Creator,
                AutomationCapabilities.Creator, true, true, null,
                [AutomationOwnerKind.Creator]),

            // Client executors. The server records them and never claims to have performed them —
            // §57's rule that a simulated or unperformed outcome must never look like a real one.
            new("item.save", "search", AutomationActionExecutor.Client,
                AutomationRepeatRisk.Safe, null, null, false, true, null, EveryBusinessOwner),
            new("open.deepLink", "notifications", AutomationActionExecutor.Client,
                AutomationRepeatRisk.Safe, null, null, false, true, null, EveryBusinessOwner),

            // Unattended calendar writing needs a configured B22 provider. The action exists and is
            // routed through B22's port; with no adapter configured it reports providerUnavailable,
            // which is the honest answer and is what GAP-025 records as an external dependency.
            new("calendar.addEvent", "events", AutomationActionExecutor.Client,
                AutomationRepeatRisk.Visible, null, null, false, true, null, EveryBusinessOwner),
            new("calendar.updateEvent", "events", AutomationActionExecutor.Client,
                AutomationRepeatRisk.Deduplicated, null, null, false, true, null, EveryBusinessOwner),
            new("calendar.removeEvent", "events", AutomationActionExecutor.Client,
                AutomationRepeatRisk.Safe, null, null, false, true, null, EveryBusinessOwner)
        }.ToFrozenDictionary(action => action.Key, StringComparer.Ordinal);

    /// <summary>Every trigger keyed on the contract it consumes. Several triggers may share one.</summary>
    public static readonly FrozenDictionary<string, IReadOnlyList<TriggerCatalogEntry>>
        TriggersBySourceEvent =
            Triggers.Values
                .Where(trigger => trigger.SourceEventName is not null)
                .GroupBy(trigger => trigger.SourceEventName!, StringComparer.Ordinal)
                .ToFrozenDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<TriggerCatalogEntry>)[.. group],
                    StringComparer.Ordinal);

    /// <summary>
    /// The triggers that are event-driven and available but name no single contract.
    /// <para>
    /// <b>The saved-search family fans in from five publishing modules</b> — a job, a listing, a
    /// housing advert, a provider and an event all produce "something new matched a search I saved" —
    /// so no one contract name belongs on the entry. They are consumed by the saved-search matcher
    /// rather than by the engine's trigger dispatch, which is why the absence is correct rather than a
    /// gap.
    /// </para>
    /// <para>
    /// Named here rather than left implicit, so the test that requires every available event-driven
    /// trigger to name a real contract can exempt exactly these and no more.
    /// </para>
    /// </summary>
    public static readonly IReadOnlySet<string> MultiSourceTriggers =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "savedSearch.newMatch",
            "savedSearch.priceChanged",
            "savedSearch.availabilityChanged"
        };

    /// <summary>
    /// The triggers that fire from a dated subject rather than from a message — Wave 12.
    /// <para>
    /// Each is <see cref="AutomationTriggerDelivery.Schedule"/> and available, and none names a
    /// contract, because the contract that <i>armed</i> it is not the thing that fires it: a job
    /// publication writes a horizon row, and the row fires a day before the posting closes. Naming
    /// <c>JobPublished</c> on the entry would tell a reader the engine consults it at fire time, which
    /// it does not.
    /// </para>
    /// <para>
    /// Named here for the same reason <see cref="MultiSourceTriggers"/> is: so the test that pairs
    /// availability with delivery exempts exactly these and no more, and a future entry cannot acquire
    /// the exemption silently.
    /// </para>
    /// </summary>
    public static readonly IReadOnlySet<string> SubjectHorizonTriggers =
        new HashSet<string>(StringComparer.Ordinal)
        {
            // Added Phase 5, when the arming for it was written. The set had gone stale the moment
            // `event.startingSoon` acquired a horizon, and nothing noticed because — despite this
            // summary naming the test it exists for — **no test read it.** One does now.
            "event.startingSoon",
            "job.closingSoon",
            "listing.expiring",
            "listing.reservationEnding",
            "housing.viewingReminder"
        };

    public static TriggerCatalogEntry? FindTrigger(string? key) =>
        key is not null && Triggers.TryGetValue(key, out var trigger) ? trigger : null;

    public static ConditionCatalogEntry? FindCondition(string? key) =>
        key is not null && Conditions.TryGetValue(key, out var condition) ? condition : null;

    public static ActionCatalogEntry? FindAction(string? key) =>
        key is not null && Actions.TryGetValue(key, out var action) ? action : null;
}
