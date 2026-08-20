using Diaspora.Contracts.Automation.V1.Extensibility;
using Diaspora.Modules.Automation;
using Diaspora.Modules.Automation.Domain.Catalog;
using Diaspora.Modules.Automation.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Automation;

/// <summary>
/// The allow-list, and the invariants that make it one.
/// <para>
/// <b>Every guarantee in this phase that is expressed as an absence is asserted here.</b> "No
/// government trigger", "no money-moving action", "reaching other people always needs a capability" —
/// each of those is a property of the catalogue's contents rather than of a check somewhere, and a
/// property of contents is only a guarantee if something reads it.
/// </para>
/// </summary>
public sealed class AutomationCatalogTests
{
    // ── The keys are the frontend's ─────────────────────────────────────────────────────────

    /// <summary>
    /// Every trigger key the frontend's catalogue offers appears here, or is deliberately absent.
    /// <para>
    /// The two government keys are the deliberate absence and the test names them, so a later phase
    /// adding a government feed has to come here and delete the assertion rather than discover the
    /// omission.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("manual.run")]
    [InlineData("event.startingSoon")]
    [InlineData("event.created")]
    [InlineData("event.updated")]
    [InlineData("event.cancelled")]
    [InlineData("event.rsvpConfirmed")]
    [InlineData("event.occurrenceChanged")]
    [InlineData("savedSearch.newMatch")]
    [InlineData("savedSearch.priceChanged")]
    [InlineData("savedSearch.availabilityChanged")]
    [InlineData("job.closingSoon")]
    [InlineData("job.applicationStatusChanged")]
    [InlineData("job.applicationSubmitted")]
    [InlineData("job.applicationThreshold")]
    [InlineData("listing.expiring")]
    [InlineData("listing.offerReceived")]
    [InlineData("listing.inquiryReceived")]
    [InlineData("listing.reservationEnding")]
    [InlineData("housing.inquiryReceived")]
    [InlineData("housing.applicationStatusChanged")]
    [InlineData("housing.viewingReminder")]
    [InlineData("community.memberJoined")]
    [InlineData("community.announcementPublished")]
    [InlineData("community.updatePublished")]
    [InlineData("community.milestoneReached")]
    [InlineData("community.eventCreated")]
    [InlineData("organization.memberJoined")]
    [InlineData("organization.registrationReceived")]
    [InlineData("organization.membershipRenewalDue")]
    [InlineData("guide.published")]
    [InlineData("guide.savedChanged")]
    [InlineData("guide.milestoneReached")]
    [InlineData("creator.milestoneReached")]
    [InlineData("creator.rewardAvailable")]
    [InlineData("creator.referralCompleted")]
    [InlineData("creator.performanceThreshold")]
    [InlineData("payment.completed")]
    [InlineData("payment.failed")]
    [InlineData("payment.refunded")]
    [InlineData("giving.donationCompleted")]
    [InlineData("subscription.expiring")]
    [InlineData("placement.ending")]
    [InlineData("provider.leadReceived")]
    [InlineData("provider.reviewReceived")]
    [InlineData("provider.featuredEnding")]
    [InlineData("conversation.inactive")]
    [InlineData("conversation.unreadReminder")]
    [InlineData("location.enteredArea")]
    [InlineData("location.countryChanged")]
    public void Every_frontend_trigger_key_is_in_the_catalogue(string key) =>
        AutomationCatalog.FindTrigger(key).Should().NotBeNull(
            "the builder offers this key and a member's saved draft must remain openable");

    [Theory]
    [InlineData("country")]
    [InlineData("community")]
    [InlineData("neighbourhood")]
    [InlineData("distance")]
    [InlineData("category")]
    [InlineData("eventType")]
    [InlineData("urgency")]
    [InlineData("dayOfWeek")]
    [InlineData("timeOfDay")]
    [InlineData("priceBelow")]
    [InlineData("priceAbove")]
    [InlineData("amountAbove")]
    [InlineData("walletBalance")]
    [InlineData("paymentKind")]
    [InlineData("countAbove")]
    [InlineData("ratingBelow")]
    [InlineData("applicationStatus")]
    [InlineData("subscriptionActive")]
    [InlineData("creatorStatus")]
    [InlineData("listingStatus")]
    [InlineData("metricAbove")]
    [InlineData("metricBelow")]
    public void Every_frontend_condition_key_is_in_the_catalogue(string key) =>
        AutomationCatalog.FindCondition(key).Should().NotBeNull();

    [Theory]
    [InlineData("notify.self")]
    [InlineData("notify.community")]
    [InlineData("notify.organization")]
    [InlineData("notify.followers")]
    [InlineData("notify.push")]
    [InlineData("notify.email")]
    [InlineData("reminder.schedule")]
    [InlineData("calendar.addEvent")]
    [InlineData("calendar.updateEvent")]
    [InlineData("calendar.removeEvent")]
    [InlineData("item.save")]
    [InlineData("open.deepLink")]
    public void Every_frontend_action_key_is_in_the_catalogue(string key) =>
        AutomationCatalog.FindAction(key).Should().NotBeNull();

    /// <summary>The catalogue offers exactly the frontend's twelve actions and no thirteenth.</summary>
    [Fact]
    public void The_action_catalogue_has_exactly_twelve_entries() =>
        AutomationCatalog.Actions.Should().HaveCount(
            12, "a thirteenth action is a capability nobody asked for");

    // ── The three absences ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Nothing that moves money.
    /// <para>
    /// The enforcement is that no such entry exists, and this is what reads it. The vocabulary is
    /// checked rather than a behaviour, because an action that could move money would have to be
    /// named before it could be implemented.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("transfer")]
    [InlineData("withdraw")]
    [InlineData("credit")]
    [InlineData("refund")]
    [InlineData("charge")]
    [InlineData("payout")]
    [InlineData("renew")]
    [InlineData("claim")]
    [InlineData("pay")]
    public void No_action_moves_money(string forbidden) =>
        AutomationCatalog.Actions.Keys
            .Should().NotContain(key => key.Contains(forbidden, StringComparison.OrdinalIgnoreCase));

    /// <summary>Nothing that sends a message on somebody's behalf.</summary>
    [Theory]
    [InlineData("message.send")]
    [InlineData("conversation.reply")]
    [InlineData("reply.auto")]
    public void No_action_sends_a_message(string forbidden) =>
        AutomationCatalog.FindAction(forbidden).Should().BeNull();

    /// <summary>Nothing that posts publicly.</summary>
    [Theory]
    [InlineData("post.publish")]
    [InlineData("content.share")]
    [InlineData("listing.publish")]
    public void No_action_posts_publicly(string forbidden) =>
        AutomationCatalog.FindAction(forbidden).Should().BeNull();

    /// <summary>
    /// No government trigger exists, because the government data source does not.
    /// <para>
    /// Wave 10 / B22.x is paused pending a separate product-scope decision. An entry marked
    /// unavailable would still be a promise that a feed exists behind it, so the vocabulary does not
    /// contain the words.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("government.advisoryUpdated")]
    [InlineData("government.noticePublished")]
    [InlineData("government.embassyChanged")]
    public void No_government_trigger_is_offered(string key) =>
        AutomationCatalog.FindTrigger(key).Should().BeNull(
            "Government Data is paused and a catalogue entry would be a promise of a feed");

    // ── The structural invariants ───────────────────────────────────────────────────────────

    /// <summary>
    /// Every action that reaches other people requires a capability.
    /// <para>
    /// The frontend asserts the same invariant on its own catalogue. Both are needed: the client's
    /// stops a control being offered, and this one stops the server accepting one.
    /// </para>
    /// </summary>
    [Fact]
    public void Reaching_other_people_always_requires_a_capability() =>
        AutomationCatalog.Actions.Values
            .Where(action => action.ReachesOtherPeople)
            .Should().OnlyContain(action => action.RequiredCapability != null)
            .And.NotBeEmpty();

    /// <summary>An action nobody may own is an action nobody may write. Every entry has an owner.</summary>
    [Fact]
    public void Every_action_names_at_least_one_owner_kind() =>
        AutomationCatalog.Actions.Values.Should().OnlyContain(action => action.AllowedOwners.Count > 0);

    /// <summary>
    /// No trigger or action may be created by a platform-wide owner.
    /// <para>
    /// §66: a normal member must not be able to create platform-wide automation, and the catalogue
    /// never offers <c>Platform</c> as an allowed owner so no rule can be written against it even if
    /// a scope check were bypassed.
    /// </para>
    /// </summary>
    [Fact]
    public void Nothing_in_the_catalogue_allows_a_platform_owner()
    {
        AutomationCatalog.Triggers.Values
            .Should().NotContain(trigger => trigger.AllowedOwners.Contains(AutomationOwnerKind.Platform));

        AutomationCatalog.Actions.Values
            .Should().NotContain(action => action.AllowedOwners.Contains(AutomationOwnerKind.Platform));
    }

    /// <summary>
    /// An unavailable entry always says why.
    /// <para>
    /// The reason is what the refusal quotes back to the member, and a refusal with no reason is a
    /// dead end for somebody who cannot see the catalogue.
    /// </para>
    /// </summary>
    [Fact]
    public void Every_unavailable_trigger_states_a_reason() =>
        AutomationCatalog.Triggers.Values
            .Where(trigger => !trigger.IsAvailable)
            .Should().OnlyContain(trigger => !string.IsNullOrWhiteSpace(trigger.UnavailableReason))
            .And.NotBeEmpty();

    [Fact]
    public void Every_unevaluable_condition_states_a_reason() =>
        AutomationCatalog.Conditions.Values
            .Where(condition => !condition.IsServerEvaluable)
            .Should().OnlyContain(condition => !string.IsNullOrWhiteSpace(condition.UnavailableReason))
            .And.NotBeEmpty();

    /// <summary>
    /// Every available event-driven trigger names a contract that really exists.
    /// <para>
    /// Resolved against the loaded assemblies by name, which is the only way to catch a typo in a
    /// namespace-qualified string. A trigger naming a contract nobody publishes would look correct in
    /// every review and match nothing at runtime.
    /// </para>
    /// </summary>
    [Fact]
    public void Every_available_trigger_names_a_real_published_contract()
    {
        var contracts = typeof(Diaspora.Contracts.Events.IIntegrationEvent).Assembly
            .GetTypes()
            .Select(type => type.FullName)
            .Where(name => name is not null)
            .ToHashSet(StringComparer.Ordinal);

        var missing = AutomationCatalog.Triggers.Values
            .Where(trigger =>
                trigger.IsAvailable &&
                trigger.Delivery == AutomationTriggerDelivery.DomainEvent &&
                trigger.SourceEventName is not null)
            .Where(trigger => !contracts.Contains(trigger.SourceEventName!))
            .Select(trigger => $"{trigger.Key} -> {trigger.SourceEventName}")
            .ToArray();

        missing.Should().BeEmpty(
            "a trigger keyed on a contract that does not exist matches nothing and fails silently");
    }

    /// <summary>
    /// A trigger that is not event-driven names no contract, and one that is names one.
    /// <para>
    /// The pairing is what keeps the catalogue readable: a scheduled trigger with a contract beside it
    /// would leave a reader unsure which of the two the engine consults.
    /// </para>
    /// </summary>
    [Fact]
    public void Availability_and_delivery_agree()
    {
        // The saved-search family is the documented exception: it fans in from five publishing
        // modules, so no one contract name belongs on the entry. Exempting it by an explicit set
        // rather than by a null check is what stops a future entry acquiring the exemption silently.
        AutomationCatalog.Triggers.Values
            .Where(trigger =>
                trigger.Delivery == AutomationTriggerDelivery.DomainEvent &&
                trigger.IsAvailable &&
                !AutomationCatalog.MultiSourceTriggers.Contains(trigger.Key))
            .Should().OnlyContain(trigger => trigger.SourceEventName != null);

        AutomationCatalog.Triggers.Values
            .Where(trigger => trigger.Delivery != AutomationTriggerDelivery.DomainEvent)
            .Should().OnlyContain(trigger => trigger.SourceEventName == null);

        AutomationCatalog.MultiSourceTriggers
            .Should().OnlyContain(key => AutomationCatalog.FindTrigger(key) != null);
    }

    /// <summary>
    /// Location automation is never granted, matching the client's own refusal.
    /// <para>
    /// The app requests only foreground location and there is no geofence backend. Both triggers stay
    /// in the catalogue so the builder can explain them, and neither is available.
    /// </para>
    /// </summary>
    [Fact]
    public void Location_triggers_are_offered_and_refused()
    {
        AutomationCatalog.FindTrigger("location.enteredArea")!.IsAvailable.Should().BeFalse();
        AutomationCatalog.FindTrigger("location.countryChanged")!.IsAvailable.Should().BeFalse();

        AutomationCatalog.FindTrigger("location.enteredArea")!.RequiredCapability
            .Should().Be(AutomationCapabilities.Location);
    }

    /// <summary>
    /// Every condition's declared operators are ones the evaluator implements.
    /// <para>
    /// A catalogue entry offering an operator the evaluator does not know would compile, validate on
    /// write, and never match at execution — the failure would look to a member like a rule that does
    /// not work rather than a defect.
    /// </para>
    /// </summary>
    [Fact]
    public void Every_declared_operator_is_one_the_evaluator_implements()
    {
        string[] implemented =
        [
            "equals", "notEquals", "includes", "excludes", "greaterThan", "lessThan",
            "between", "within", "isTrue", "isFalse"
        ];

        AutomationCatalog.Conditions.Values
            .SelectMany(condition => condition.Operators)
            .Distinct(StringComparer.Ordinal)
            .Should().OnlyContain(op => implemented.Contains(op, StringComparer.Ordinal));
    }
}
