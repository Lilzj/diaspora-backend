using System.Globalization;
using Diaspora.Contracts.Conversations.V1.Events;
using Diaspora.Contracts.Marketplace.V1.Events;
using Diaspora.Modules.Automation.Application.Engine;
using Diaspora.Modules.Automation.Application.Services;
using Diaspora.Modules.Automation.Domain.Catalog;
using Diaspora.Modules.Automation.Domain.Enums;

namespace Diaspora.Modules.Automation.Application.Consumers;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// FRONTEND-PHASE18-GAP-011 · GAP-012 · GAP-020 — the three "somebody got in touch" triggers.
// W12-GAP-006 · 007 · 008.
//
// Wave 11 recorded all three as owning-module responsibilities because no contract is *named*
// `ListingInquiryReceived`, `HousingInquiryReceived` or `ProviderLeadReceived`. But an enquiry on
// this platform is not a Marketplace concept, a Housing concept and a Providers concept implemented
// three times — it is **a conversation**, and B13.2 owns it. `ConversationCreatedIntegrationEvent`
// carries the context type, the entity it is about and who is in it, and B13.2's own context
// catalogue seeds `MarketplaceListing`, `HousingListing`, `ProviderInquiry` and `BusinessAccount`
// with the owning module named on each.
//
// So one consumer answers all three, and asking Marketplace, Housing and Providers each to publish a
// parallel enquiry contract would have produced three copies of a fact B13.2 already publishes —
// and three places for the mute, block and privacy rules that conversation carries to be forgotten.
//
// **No message content reaches this file, and there is none to reach it.** The contract carries no
// body, by B13.2's own design; the frontend's promise on all three triggers is the same — "the
// message itself stays in your inbox" — and it is kept structurally rather than by a check here.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Somebody opened a conversation about something a member owns.
/// <para>
/// <b>One contract, three triggers, and the context type chooses which.</b> The alternative — three
/// consumers over one contract — is what DSP1003 reports: three settlement owners inside one module
/// for one event, so an at-least-once redelivery would apply three times.
/// </para>
/// <para>
/// <b>Who it concerns is every participant except the person who started it.</b> An enquiry is
/// somebody getting in touch with somebody else, and the initiator's own automation must not fire on
/// their own message — a buyer who wrote "tell me when I get an enquiry" would otherwise be told about
/// every enquiry they sent. Deriving it from the participant list rather than from a seller field is
/// also what keeps this correct for a conversation with more than two people in it.
/// </para>
/// <para>
/// <b>The initiator is never named in the payload.</b> The frontend's wording is "it tells you one
/// arrived — who it is from stays in your inbox", and a notification that named the sender would be
/// the automation platform disclosing a participant before the member has opened the thread.
/// </para>
/// </summary>
public sealed class ConversationEnquiryAutomationConsumer
    : AutomationEventConsumer<ConversationCreatedIntegrationEvent>
{
    public ConversationEnquiryAutomationConsumer(AutomationEngine engine) : base(engine) { }

    /// <summary>
    /// The context types that are an enquiry about something, and the trigger each produces.
    /// <para>
    /// Keyed on B13.2's own seeded context keys. A context type this map does not name — a private
    /// conversation, a support ticket, a guide, an event — produces nothing, which is correct: those
    /// are conversations rather than enquiries about a listing somebody owns.
    /// </para>
    /// <para>
    /// <c>BusinessAccount</c> maps to the provider trigger alongside <c>ProviderInquiry</c> because
    /// B13.2 seeds both with <c>OwningModule = "Providers"</c> and both are somebody getting in touch
    /// through a provider's profile, which is the frontend's own definition of a lead.
    /// </para>
    /// </summary>
    private static readonly Dictionary<string, (string Trigger, string SubjectKind)> Enquiries =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["MarketplaceListing"] = ("listing.inquiryReceived", AutomationSubjectKinds.Listing),
            ["HousingListing"] = ("housing.inquiryReceived", AutomationSubjectKinds.Housing),
            ["ProviderInquiry"] = ("provider.leadReceived", AutomationSubjectKinds.Provider),
            ["BusinessAccount"] = ("provider.leadReceived", AutomationSubjectKinds.Provider)
        };

    protected override IEnumerable<AutomationTriggerContext> Translate(
        ConversationCreatedIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (!Enquiries.TryGetValue(integrationEvent.ContextType, out var enquiry))
        {
            yield break;
        }

        // A pending request is somebody asking to be allowed to talk, and B13.2 has not let them
        // through yet. Firing an automation on it would route around the acceptance gate that context
        // type exists to impose. `ConversationRequestAccepted` is the fact for that case, and no
        // enquiry trigger is offered against it.
        if (!string.Equals(integrationEvent.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        var recipients = integrationEvent.ParticipantUserIds
            .Where(participant =>
                participant != Guid.Empty && participant != integrationEvent.InitiatorUserId)
            .Distinct()
            .ToArray();

        if (recipients.Length == 0)
        {
            yield break;
        }

        var values = Values();

        // The entity identifier is opaque to Conversations and is the owning module's own. Parsed
        // where it is a GUID — every one of the three owning modules uses one — and left out where it
        // is not, rather than guessed at.
        var subjectId = Guid.TryParse(integrationEvent.EntityId, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : (Guid?)null;

        yield return Context(
            enquiry.Trigger,
            integrationEvent.EventId,
            recipients,

            // No name, no listing title, no message. "Somebody" is the whole of what an enquiry
            // notification may say before the member opens it.
            enquiry.Trigger switch
            {
                "housing.inquiryReceived" => "Somebody enquired about your property.",
                "provider.leadReceived" => "Somebody got in touch through your profile.",
                _ => "Somebody asked about your listing."
            },
            integrationEvent.CreatedAt,
            enquiry.SubjectKind,
            subjectId,
            null,
            null,
            null,
            values);
    }
}

// ════════════════════════════════════════════════════════════════════════════════════════════════
// FRONTEND-PHASE18-GAP-021 — the two messaging triggers. W12-GAP-012 · 013.
//
// Wave 11 refused both and was right to: "deriving inactivity would mean this module reading another
// module's message state. Conversations owns that and must publish the fact." Wave 12 had B13.2
// publish it, and these two consumers are the whole of this module's involvement.
//
// **No message content reaches either, and there is none to reach them.** Both contracts carry
// timestamps, counts and identifiers by construction — the frontend's promise on
// `conversation.unreadReminder` is "nothing about their contents is read", and it is kept by there
// being no field it could be read from.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// A conversation somebody is part of has gone quiet.
/// <para>
/// One event per participant who is owed a reply — B13.2 decides who that is, because mute, archive
/// and departure are its state and no other module has heard of them.
/// </para>
/// </summary>
public sealed class ConversationInactivityAutomationConsumer
    : AutomationEventConsumer<ConversationInactivityDetectedIntegrationEvent>
{
    public ConversationInactivityAutomationConsumer(AutomationEngine engine) : base(engine) { }

    protected override IEnumerable<AutomationTriggerContext> Translate(
        ConversationInactivityDetectedIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (integrationEvent.ParticipantUserId == Guid.Empty)
        {
            yield break;
        }

        var values = Values();
        values["countAbove"] = integrationEvent.QuietHours.ToString(CultureInfo.InvariantCulture);

        yield return Context(
            "conversation.inactive",
            integrationEvent.EventId,
            Owners(integrationEvent.ParticipantUserId),

            // The context title, which is public wherever the entity is — a listing's title, a
            // property's. Never the other participant, and never anything they wrote.
            integrationEvent.ContextTitle is { Length: > 0 } title
                ? $"Your conversation about {title} has gone quiet."
                : "A conversation you are part of has gone quiet.",
            integrationEvent.OccurredAt,
            AutomationSubjectKinds.Conversation,
            integrationEvent.ConversationId,
            integrationEvent.ContextTitle,
            null,
            null,
            values);
    }
}

/// <summary>
/// A member has messages sitting unread.
/// <para>
/// One event per member rather than per thread — the trigger is "I have unread messages", and nine
/// threads producing nine runs would be nine notifications about one state.
/// </para>
/// </summary>
public sealed class ConversationUnreadAutomationConsumer
    : AutomationEventConsumer<ConversationUnreadThresholdReachedIntegrationEvent>
{
    public ConversationUnreadAutomationConsumer(AutomationEngine engine) : base(engine) { }

    protected override IEnumerable<AutomationTriggerContext> Translate(
        ConversationUnreadThresholdReachedIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (integrationEvent.MemberUserId == Guid.Empty)
        {
            yield break;
        }

        var values = Values();

        // The count the frontend's `countAbove` condition compares on this trigger. A figure the
        // publisher computed, which is the only kind this module's analytics conditions accept.
        values["countAbove"] = integrationEvent.UnreadCount.ToString(CultureInfo.InvariantCulture);

        yield return Context(
            "conversation.unreadReminder",
            integrationEvent.EventId,
            Owners(integrationEvent.MemberUserId),
            integrationEvent.UnreadCount == 1
                ? "You have an unread message."
                : $"You have {integrationEvent.UnreadCount} unread messages.",
            integrationEvent.OccurredAt,
            AutomationSubjectKinds.Conversation,

            // No subject: the backlog is across an inbox rather than about one thread, and naming one
            // would deep-link a member into whichever conversation happened to sort first.
            null,
            null,
            null,
            null,
            values);
    }
}

// ════════════════════════════════════════════════════════════════════════════════════════════════
// FRONTEND-PHASE18-GAP-009 — the price-changed half of the saved-search family. W12-GAP-001.
//
// Wave 11 recorded "Marketplace and Housing publish no price-changed contract". Marketplace does:
// `ListingUpdatedIntegrationEvent` carries `ChangedFields`, `PriceAmount` and `PreviousPriceAmount`,
// and its own summary says why — "the new price travels with the event when the price is what moved…
// price-watch notifications are the single most-requested marketplace feature".
//
// Housing genuinely did not, and Wave 12 adds `HousingUpdatedIntegrationEvent` to close that half.
// Both consumers are here, side by side, so the two halves of one trigger cannot drift.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// A listing's content changed, and the price is what moved.
/// <para>
/// <b>Only when <c>price</c> is among the changed fields.</b> That is the whole reason the publishing
/// contract carries the list: a consumer that could see only "something changed" would notify on every
/// caption edit, and a member watching a price would learn to ignore it.
/// </para>
/// <para>
/// <b>And only when the price actually moved.</b> A republication that reports <c>price</c> with the
/// same figure on both sides is an edit that touched the field without changing it, and "the price
/// changed" about a price that did not is the same wrong notification by a slower route.
/// </para>
/// <para>
/// <b>And only to somebody watching it — 21 Oct 2026.</b> This passed an empty owner set, which the
/// engine reads as "everybody": a draft's price reached every member's price-watch automation, and a
/// published one reached members whose saved searches had nothing to do with it. Now the listing must
/// be one a stranger could find (<c>IsListedPublicly</c>, Marketplace's own rule; a message without
/// it fails closed), the price must be one the seller discloses, and the owners are the members with
/// an alerts-enabled marketplace saved search the listing matches. No such member, no run — never an
/// empty owner set.
/// </para>
/// </summary>
public sealed class ListingPriceChangedAutomationConsumer
    : AutomationEventConsumer<ListingUpdatedIntegrationEvent>
{
    private readonly SavedSearchMatcher _matcher;

    public ListingPriceChangedAutomationConsumer(AutomationEngine engine, SavedSearchMatcher matcher)
        : base(engine) => _matcher = matcher;

    /// <summary>Nothing: the owner set needs the saved-search read in <see cref="HandleAsync"/>.</summary>
    protected override IEnumerable<AutomationTriggerContext> Translate(
        ListingUpdatedIntegrationEvent integrationEvent) => [];

    protected override async Task HandleAsync(
        ListingUpdatedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (!integrationEvent.IsListedPublicly || !integrationEvent.IsPriceDisclosed)
        {
            // A draft, an archived, private or hidden listing, or a price the seller does not publish:
            // nobody but the seller may hear about it.
            return;
        }

        if (!integrationEvent.ChangedFields.Contains("price", StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        if (integrationEvent.PriceAmount is not { } price)
        {
            // The field moved but the module published no figure. There is nothing a price condition
            // could be compared against, and inventing one would be the platform reporting a number
            // the seller chose not to publish.
            return;
        }

        if (integrationEvent.PreviousPriceAmount is { } previous && previous == price)
        {
            return;
        }

        // The same fields `ListingPublished` offers a new-match comparison, so a search matches a
        // price change exactly when it would have matched the listing.
        var fields = PriceChangeFields.New();
        fields["country"] = integrationEvent.CountryCode;
        fields["category"] = integrationEvent.CategoryKey;
        fields["city"] = integrationEvent.CityName;
        fields["region"] = integrationEvent.RegionName;
        fields["listingType"] = integrationEvent.ListingType;
        fields["condition"] = integrationEvent.Condition;
        fields["price"] = price.ToString(CultureInfo.InvariantCulture);

        var owners = await _matcher.ListMatchingMembersAsync(
            SavedSearchSubject.Marketplace,
            new SavedSearchCandidate(integrationEvent.ListingId, string.Empty, fields, null),
            cancellationToken);

        if (owners.Count == 0)
        {
            return;
        }

        var values = Values();
        values["priceAbove"] = price.ToString(CultureInfo.InvariantCulture);
        values["priceBelow"] = price.ToString(CultureInfo.InvariantCulture);

        await Engine.ObserveAsync(
            Context(
                "savedSearch.priceChanged",
                integrationEvent.EventId,
                owners,
                integrationEvent.PreviousPriceAmount is { } before && before > price
                    ? "The price dropped on something you are watching."
                    : "The price changed on something you are watching.",
                integrationEvent.ChangedAt,
                AutomationSubjectKinds.Listing,
                integrationEvent.ListingId,
                null,
                null,
                null,
                values),
            cancellationToken);
    }
}

/// <summary>
/// A property's content changed, and the rent is what moved.
/// <para>
/// The housing half of the same trigger, over Wave 12's <c>HousingUpdatedIntegrationEvent</c>. Same
/// guards as the marketplace half — the field must be among the changed ones, and the figure must
/// have been disclosed — plus the disclosure flag, which Housing carries separately because a
/// landlord withholding the rent is the common case rather than the exception.
/// </para>
/// <para>
/// <b>21 Oct 2026</b>, as the marketplace half: only a listing a stranger could find
/// (<c>IsListedPublicly</c>, Housing's own rule), and only the members with an alerts-enabled housing
/// saved search it matches. Nobody else, and never an empty owner set.
/// </para>
/// </summary>
public sealed class HousingPriceChangedAutomationConsumer
    : AutomationEventConsumer<global::Diaspora.Contracts.Housing.V1.Events.HousingUpdatedIntegrationEvent>
{
    private readonly SavedSearchMatcher _matcher;

    public HousingPriceChangedAutomationConsumer(AutomationEngine engine, SavedSearchMatcher matcher)
        : base(engine) => _matcher = matcher;

    /// <summary>Nothing: the owner set needs the saved-search read in <see cref="HandleAsync"/>.</summary>
    protected override IEnumerable<AutomationTriggerContext> Translate(
        global::Diaspora.Contracts.Housing.V1.Events.HousingUpdatedIntegrationEvent integrationEvent) => [];

    protected override async Task HandleAsync(
        global::Diaspora.Contracts.Housing.V1.Events.HousingUpdatedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (!integrationEvent.IsListedPublicly)
        {
            return;
        }

        // The aggregate names its fields in PascalCase — `nameof(Price)` — and Marketplace names its
        // equivalent in lower case. Compared case-insensitively rather than re-cased on the way out,
        // so the published list keeps agreeing with the one B11's own handlers read.
        if (!integrationEvent.ChangedFields.Contains("Price", StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        if (!integrationEvent.IsRentDisclosed || integrationEvent.RentAmount is not { } rent)
        {
            return;
        }

        if (integrationEvent.PreviousRentAmount is { } previous && previous == rent)
        {
            return;
        }

        // The fields `HousingPublished` offers a new-match comparison, plus the country and area the
        // update carries.
        var fields = PriceChangeFields.New();
        fields["accommodationType"] = integrationEvent.AccommodationTypeKey;
        fields["bedrooms"] = integrationEvent.Bedrooms?.ToString(CultureInfo.InvariantCulture);
        fields["furnished"] = integrationEvent.FurnishedStatus;
        fields["shortStay"] = integrationEvent.AcceptsShortStay ? "true" : "false";
        fields["studentFriendly"] = integrationEvent.IsStudentFriendly ? "true" : "false";
        fields["rent"] = rent.ToString(CultureInfo.InvariantCulture);
        fields["price"] = rent.ToString(CultureInfo.InvariantCulture);
        fields["country"] = integrationEvent.CountryCode;
        fields["neighbourhood"] = integrationEvent.NeighborhoodAreaId?.ToString("D");

        var owners = await _matcher.ListMatchingMembersAsync(
            SavedSearchSubject.Housing,
            new SavedSearchCandidate(integrationEvent.ListingId, string.Empty, fields, null),
            cancellationToken);

        if (owners.Count == 0)
        {
            return;
        }

        var values = Values();
        values["priceAbove"] = rent.ToString(CultureInfo.InvariantCulture);
        values["priceBelow"] = rent.ToString(CultureInfo.InvariantCulture);
        values["country"] = integrationEvent.CountryCode;
        values["neighbourhood"] = integrationEvent.NeighborhoodAreaId?.ToString("D");

        await Engine.ObserveAsync(
            Context(
                "savedSearch.priceChanged",
                integrationEvent.EventId,
                owners,
                integrationEvent.PreviousRentAmount is { } before && before > rent
                    ? "The rent dropped on somewhere you are watching."
                    : "The rent changed on somewhere you are watching.",
                integrationEvent.ChangedAt,
                AutomationSubjectKinds.Housing,
                integrationEvent.ListingId,
                null,
                null,
                null,
                values),
            cancellationToken);
    }
}

/// <summary>The saved-search field bag both price consumers build, with the matcher's comparer.</summary>
internal static class PriceChangeFields
{
    public static Dictionary<string, string?> New() => new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// A seeker's application on a property moved — W12-GAP-011.
/// <para>
/// <b>Concerns the seeker, and only the seeker.</b> The frontend scopes this trigger to <c>user</c>
/// and describes it as "my housing application status changes"; the landlord moved it, and telling
/// them their own action happened is not a notification.
/// </para>
/// <para>
/// The status is server-evaluated for the same reason a job application's is, and the frontend's
/// condition catalogue gives it: the client's copy can be stale, and a rule firing on a stale copy is
/// a member told a landlord agreed when they did not.
/// </para>
/// </summary>
public sealed class HousingApplicationStatusAutomationConsumer
    : AutomationEventConsumer<global::Diaspora.Contracts.Housing.V1.Events.HousingApplicationStatusChangedIntegrationEvent>
{
    public HousingApplicationStatusAutomationConsumer(AutomationEngine engine) : base(engine) { }

    protected override IEnumerable<AutomationTriggerContext> Translate(
        global::Diaspora.Contracts.Housing.V1.Events.HousingApplicationStatusChangedIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (integrationEvent.SeekerUserId == Guid.Empty)
        {
            yield break;
        }

        var values = Values();
        values["applicationStatus"] = integrationEvent.CurrentStatus;

        yield return Context(
            "housing.applicationStatusChanged",
            integrationEvent.EventId,
            Owners(integrationEvent.SeekerUserId),

            // The status is a public fact about the member's own application, so the summary may name
            // it — the same rule Wave 11's job-application consumer states. What never appears is a
            // sensitive condition's comparison, and that is enforced in the run record.
            $"Your housing application moved to {integrationEvent.CurrentStatus}.",
            integrationEvent.ChangedAt,
            AutomationSubjectKinds.Application,
            integrationEvent.ReservationId,
            null,
            null,
            null,
            values);
    }
}
