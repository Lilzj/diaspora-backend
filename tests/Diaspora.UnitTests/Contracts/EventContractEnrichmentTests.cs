using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diaspora.Contracts.CommunityLife.V1.Events;
using Diaspora.Contracts.Events;
using Diaspora.Contracts.Guides.V1.Events;
using Diaspora.Contracts.Jobs.V1.Events;
using Diaspora.Contracts.Providers.V1.Events;
using Diaspora.Platform.Messaging.Outbox.Serialization;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Contracts;

/// <summary>
/// B13.1's whole claim, stated as tests: thirty-nine properties were added to thirty-five
/// published contracts, and nothing that was already on the wire changed.
/// <para>
/// <b>The inventory below is the phase, written down.</b> It is hard-coded rather than discovered
/// by reflection, and that is deliberate: a rule of the form "every property called
/// <c>OrganizerUserId</c> is nullable" would be false — Community Life has carried a required one
/// on three contracts since B12 — and a test that derived the list from the code would agree with
/// whatever the code did. This list is the specification, and the Event Contract Enrichment Guide
/// documents the same thirty-nine.
/// </para>
/// <para>
/// <b>The failure these guard against is invisible in a build and loud in production.</b> An
/// enrichment declared <c>required</c> compiles, passes every test that constructs one, and then
/// throws on the first replayed row written before the field existed — hours after the deploy,
/// in a dead-letter queue, with a message about a missing property nobody remembers adding.
/// </para>
/// </summary>
public sealed class EventContractEnrichmentTests
{
    /// <summary>
    /// Every property B13.1 added, and the contract it was added to.
    /// <para>
    /// Grouped by publishing module, because ownership is the rule the phase exists to state:
    /// each of these is written by the module that owns the fact, and by nobody else.
    /// </para>
    /// </summary>
    public static readonly (Type Contract, string Property)[] Enrichments =
    [
        // ── Jobs: the applying member and the employer's notifiable member ──────────────────
        (typeof(ApplicationSubmittedIntegrationEvent), "EmployerUserId"),
        (typeof(ApplicationStatusChangedIntegrationEvent), "CandidateUserId"),
        (typeof(ApplicationStatusChangedIntegrationEvent), "EmployerUserId"),
        (typeof(ApplicationWithdrawnIntegrationEvent), "CandidateUserId"),
        (typeof(ApplicationWithdrawnIntegrationEvent), "EmployerUserId"),
        (typeof(CandidateShortlistedIntegrationEvent), "EmployerUserId"),
        (typeof(InterviewScheduledIntegrationEvent), "EmployerUserId"),
        (typeof(OfferExtendedIntegrationEvent), "EmployerUserId"),
        (typeof(OfferAnsweredIntegrationEvent), "CandidateUserId"),
        (typeof(OfferAnsweredIntegrationEvent), "EmployerUserId"),
        (typeof(OfferAcceptedIntegrationEvent), "CandidateUserId"),
        (typeof(OfferAcceptedIntegrationEvent), "EmployerUserId"),
        (typeof(JobClosedIntegrationEvent), "EmployerUserId"),
        (typeof(JobArchivedIntegrationEvent), "EmployerUserId"),
        (typeof(EmployerVerifiedIntegrationEvent), "EmployerUserId"),

        // ── Community Life: the organizer, and the body's registered administrator ──────────
        (typeof(EventCancelledIntegrationEvent), "OrganizerUserId"),
        (typeof(EventArchivedIntegrationEvent), "OrganizerUserId"),
        (typeof(EventReportedIntegrationEvent), "OrganizerUserId"),
        (typeof(RsvpSubmittedIntegrationEvent), "OrganizerUserId"),
        (typeof(RsvpWithdrawnIntegrationEvent), "OrganizerUserId"),
        (typeof(OrganizationPublishedIntegrationEvent), "OrganizerUserId"),
        (typeof(OrganizationArchivedIntegrationEvent), "OrganizerUserId"),
        (typeof(OrganizationVerifiedIntegrationEvent), "OrganizerUserId"),
        (typeof(OrganizationReportedIntegrationEvent), "OrganizerUserId"),
        (typeof(OrganizationMemberJoinedIntegrationEvent), "OrganizerUserId"),
        (typeof(OrganizationMemberLeftIntegrationEvent), "OrganizerUserId"),

        // ── Guides: the author, on every contract that lacked one ───────────────────────────
        (typeof(GuideUpdatedIntegrationEvent), "AuthorUserId"),
        (typeof(GuideArchivedIntegrationEvent), "AuthorUserId"),
        (typeof(GuideRestoredIntegrationEvent), "AuthorUserId"),
        (typeof(GuideFeaturedIntegrationEvent), "AuthorUserId"),
        (typeof(GuideVerificationChangedIntegrationEvent), "AuthorUserId"),
        (typeof(GuideVersionCreatedIntegrationEvent), "AuthorUserId"),
        (typeof(GuideViewMilestoneReachedIntegrationEvent), "AuthorUserId"),
        (typeof(GuideCommentAddedIntegrationEvent), "AuthorUserId"),

        // ── Providers: the business's owner ─────────────────────────────────────────────────
        (typeof(ProviderUpdatedIntegrationEvent), "OwnerUserId"),
        (typeof(ProviderStatusChangedIntegrationEvent), "OwnerUserId"),
        (typeof(ProviderVerificationChangedIntegrationEvent), "OwnerUserId"),
        (typeof(ProviderReviewReceivedIntegrationEvent), "OwnerUserId"),
        (typeof(ProviderRecommendationReceivedIntegrationEvent), "OwnerUserId")
    ];

    /// <summary>
    /// Nullable member identifiers that already existed, and are therefore not enrichments.
    /// <para>
    /// One entry, and it is worth naming rather than pattern-matching around: B12's check-in
    /// contract has always carried who worked the door, null when the attendee confirmed their
    /// own arrival. It is an actor, not a recipient — which is exactly why the completeness rule
    /// below cannot simply assume every nullable <c>…UserId</c> in these four namespaces arrived
    /// with this phase.
    /// </para>
    /// </summary>
    private static readonly (Type Contract, string Property)[] PredatingB131 =
    [
        (typeof(EventCheckInCompletedIntegrationEvent), "RecordedByUserId")
    ];

    /// <summary>
    /// Nullable member identifiers a contract was <em>born</em> with, on a contract published after
    /// B13.1.
    /// <para>
    /// A third category, and it needs naming for the same reason <see cref="PredatingB131"/> did:
    /// the completeness rule below cannot assume every nullable <c>…UserId</c> in these namespaces
    /// arrived as an enrichment. These two contracts are B12.1's, and both identifiers were on them
    /// from their first line — <c>OrganizerUserId</c> because ADR-014 says a published event carries
    /// enough context to act alone, and <c>ChangedByUserId</c> because who moved a date is the one
    /// thing a "the meeting has moved" message cannot be written without.
    /// </para>
    /// <para>
    /// Neither is an enrichment, so neither belongs in <see cref="Enrichments"/>: nothing was added
    /// to an existing contract, and no consumer has ever seen these contracts without them.
    /// </para>
    /// <para>
    /// <b>The two guide contracts of 13 Sep 2026 are the same case.</b> A guide share and a guide
    /// vote had never crossed the module boundary; both contracts were written with
    /// <c>AuthorUserId</c> on their first line, so analytics can attribute the act to the guide's
    /// author. Nullable, as every guide contract's author is.
    /// </para>
    /// </summary>
    private static readonly (Type Contract, string Property)[] BornWithTheContract =
    [
        (typeof(EventUpdatedIntegrationEvent), "ChangedByUserId"),
        (typeof(EventUpdatedIntegrationEvent), "OrganizerUserId"),
        (typeof(EventOccurrenceRescheduledIntegrationEvent), "ChangedByUserId"),
        (typeof(EventOccurrenceRescheduledIntegrationEvent), "OrganizerUserId"),
        (typeof(GuideSharedIntegrationEvent), "AuthorUserId"),
        (typeof(GuideVoteCastIntegrationEvent), "AuthorUserId")
    ];

    /// <summary>
    /// A fourth category: an identifier a later phase appended so a consumer could say <em>whose
    /// work this was</em>, rather than so a notification could reach somebody.
    /// <para>
    /// BG-10 found that creator activity was being recorded against the <em>profile</em> id rather
    /// than the member id, so a member's own contributions were attributed to a row that was not
    /// them. The fix needs the publishing module to state the owning member on the event, because
    /// Creator cannot resolve one without reaching into a module it may not depend on.
    /// </para>
    /// <para>
    /// It does not belong in <see cref="Enrichments"/>, and the distinction is the reason this list
    /// exists rather than a fourth line added there. Those are B13.1's <em>recipient</em>
    /// identifiers — who a message is delivered to — and the relay fills them from
    /// <c>NotifiableOwnerUserId</c>, which is deliberately null for an owner who has switched
    /// delivery off. This is attribution: it comes from <c>OwnerUserId</c> and is present whatever
    /// the owner's notification preferences say, because who wrote something does not change when
    /// they mute it.
    /// </para>
    /// <para>
    /// <c>HousingPublishedIntegrationEvent.LandlordUserId</c> is the same identifier for the same
    /// reason, and is not listed because <c>Diaspora.Contracts.Housing</c> is not one of the four
    /// namespaces the completeness rule below scans. Named here so the pair is discoverable from
    /// either half.
    /// </para>
    /// </summary>
    private static readonly (Type Contract, string Property)[] AttributionIdentifiers =
    [
        (typeof(JobPublishedIntegrationEvent), "EmployerUserId")
    ];

    /// <summary>
    /// What B12.1 appended to three event contracts, so the append-order rule can tell them from
    /// the properties those contracts carried before B13.1.
    /// <para>
    /// <b>PSR-1 established that this list has to exist</b>, and its reasoning is in
    /// <see cref="The_enrichments_are_appended_rather_than_inserted"/>: a later phase's appended
    /// field is not "original", and counting it as one fails a contract that has not moved a single
    /// property. B12.1 is the second phase to append after B13.1, and it is recorded here for the
    /// same reason PSR-1's two were.
    /// </para>
    /// </summary>
    public static readonly (Type Contract, string Property)[] SeriesEnrichments =
    [
        // Which repeating event generated the date, so a consumer can group twenty-six cards into
        // one — and, on the cancellation, tell "the meetup is off" from "this week's meetup is off".
        (typeof(EventCreatedIntegrationEvent), "SeriesId"),
        (typeof(EventCreatedIntegrationEvent), "SeriesSequence"),
        (typeof(EventPublishedIntegrationEvent), "SeriesId"),
        (typeof(EventPublishedIntegrationEvent), "SeriesSequence"),
        (typeof(EventCancelledIntegrationEvent), "SeriesId"),
        (typeof(EventCancelledIntegrationEvent), "IsSeriesCancellation")
    ];

    /// <summary>
    /// What B13.3 appended to the RSVP contract, recorded here for the same reason B12.1's are.
    /// <para>
    /// <b>This list is the maintenance cost of the append-order rule, and paying it is the rule
    /// working.</b> B13.3 appended two fields after B13.1's <c>OrganizerUserId</c>; without this
    /// entry the rule counts them as properties that predate B13.1, concludes that
    /// <c>OrganizerUserId</c> was moved in front of them, and fails a contract whose declaration
    /// order has not changed. The failure is the rule doing exactly what it was built to do — it
    /// noticed two new properties on an enriched contract — and the correct response is to say which
    /// phase added them rather than to relax the comparison.
    /// </para>
    /// <para>
    /// Both exist so an event reminder can be scheduled at all: without a start there is no moment to
    /// apply a rule's offset to, and the dispatcher's documented answer to a scheduled rule with no
    /// moment is to send immediately.
    /// </para>
    /// </summary>
    public static readonly (Type Contract, string Property)[] ReminderEnrichments =
    [
        (typeof(RsvpSubmittedIntegrationEvent), "EventStartsAt"),
        (typeof(RsvpSubmittedIntegrationEvent), "EventTitle")
    ];

    /// <summary>
    /// What 21 Oct 2026 appended to the event contracts an automation reads: whether a stranger could
    /// have found the event, which decides whether anybody but its host is told about it. Recorded for
    /// the same reason B12.1's are.
    /// </summary>
    public static readonly (Type Contract, string Property)[] AudienceEnrichments =
    [
        (typeof(EventPublishedIntegrationEvent), "IsListedPublicly"),
        (typeof(EventUpdatedIntegrationEvent), "IsListedPublicly"),
        (typeof(EventCancelledIntegrationEvent), "IsListedPublicly"),
        (typeof(EventOccurrenceRescheduledIntegrationEvent), "IsListedPublicly")
    ];

    public static TheoryData<Type, string> Cases()
    {
        var data = new TheoryData<Type, string>();

        foreach (var (contract, property) in Enrichments)
        {
            data.Add(contract, property);
        }

        return data;
    }

    private static readonly Guid PinnedRecipient =
        Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");

    /// <summary>
    /// A plain reader, with none of the outbox serializer's converters. Standing in for a
    /// consumer that parses a payload with whatever it happens to have.
    /// </summary>
    private static readonly JsonSerializerOptions PlainReader =
        new() { PropertyNameCaseInsensitive = true };

    private static JsonOutboxSerializer Serializer() =>
        new(new IntegrationEventTypeResolver([typeof(IIntegrationEvent).Assembly]));

    // ── The shape of an enrichment ───────────────────────────────────────

    /// <summary>
    /// <b>The rule the whole phase rests on.</b> <c>System.Text.Json</c> honours C#'s
    /// <c>required</c>: a payload missing a required property throws rather than defaulting. An
    /// enrichment declared required would therefore be undetectable until the first historical
    /// row replayed, and would then fail every one of them.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void Every_enrichment_is_an_optional_nullable_identifier(Type contract, string property)
    {
        var declared = contract.GetProperty(property);

        declared.Should().NotBeNull("{0} declares {1}", contract.Name, property);

        declared!.PropertyType.Should().Be<Guid?>(
            "{0}.{1} must be nullable — a contract that cannot express 'nobody' forces a caller " +
            "to invent an identifier, which is how Guid.Empty ended up addressing three of the " +
            "platform's notifications",
            contract.Name,
            property);

        declared.GetCustomAttributes()
            .Should().NotContain(
                attribute => attribute.GetType().Name == "RequiredMemberAttribute",
                "{0}.{1} must not be required, or every payload written before B13.1 fails to " +
                "deserialize",
                contract.Name,
                property);
    }

    /// <summary>
    /// Immutable, like every other property on a published contract. A settable one would let a
    /// consumer rewrite the recipient of an event it received.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void Every_enrichment_is_init_only(Type contract, string property)
    {
        var setter = contract.GetProperty(property)!.SetMethod;

        setter.Should().NotBeNull();

        setter!.ReturnParameter.GetRequiredCustomModifiers()
            .Should().Contain(
                type => type.Name == "IsExternalInit",
                "{0}.{1} must be init-only", contract.Name, property);
    }

    // ── Construction and serialization ───────────────────────────────────

    /// <summary>
    /// The constructor carries it, and the wire carries it. Both halves matter: a property that
    /// can be set but is not serialized is a field the publisher believes it sent.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void An_enrichment_set_on_construction_reaches_the_payload(Type contract, string property)
    {
        var integrationEvent = Build(contract, withEnrichments: true);

        contract.GetProperty(property)!.GetValue(integrationEvent)
            .Should().Be(PinnedRecipient);

        var payload = JsonNode.Parse(Serializer().Serialize(integrationEvent).Payload)!.AsObject();

        payload.ContainsKey(property).Should().BeTrue(
            "{0} must write {1} to the wire, not merely hold it", contract.Name, property);

        payload[property]!.GetValue<string>().Should().Be(PinnedRecipient.ToString());
    }

    /// <summary>
    /// A full round trip through the real outbox serializer, not a hand-rolled one — because the
    /// converter set that resolves strongly typed identifiers lives on that serializer and
    /// nowhere else.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void An_enrichment_survives_a_round_trip(Type contract, string property)
    {
        var serializer = Serializer();
        var original = Build(contract, withEnrichments: true);

        var restored = serializer.Deserialize(serializer.Serialize(original));

        restored.Should().BeOfType(contract);
        contract.GetProperty(property)!.GetValue(restored).Should().Be(PinnedRecipient);
        restored.EventId.Should().Be(original.EventId);
        restored.OccurredAt.Should().Be(original.OccurredAt);
    }

    // ── Backward compatibility ───────────────────────────────────────────

    /// <summary>
    /// <b>The compatibility claim, tested against the thing it is a claim about.</b> The payload
    /// here is built without the enrichment at all — the exact bytes a pre-B13.1 build wrote —
    /// and it has to deserialize into today's type without complaint.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void A_payload_written_before_the_enrichment_still_deserializes(
        Type contract, string property)
    {
        var historical = Build(contract, withEnrichments: false);

        contract.GetProperty(property)!.GetValue(historical)
            .Should().BeNull(
                "a replayed payload that never carried {0} must read as 'nobody', not as an " +
                "identifier somebody could be sent a message at",
                property);
    }

    /// <summary>
    /// Null survives the round trip as null. The failure this catches is a converter or a default
    /// that turns an absent recipient into <see cref="Guid.Empty"/> — which is not absence, and
    /// is precisely the bug B13.1 fixed in three Jobs contracts.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void A_null_enrichment_stays_null_through_the_outbox(Type contract, string property)
    {
        var serializer = Serializer();
        var historical = Build(contract, withEnrichments: false);

        var restored = serializer.Deserialize(serializer.Serialize(historical));

        contract.GetProperty(property)!.GetValue(restored).Should().BeNull();
    }

    /// <summary>
    /// The other direction: a consumer running pre-B13.1 code meets a payload that gained a
    /// field. <c>UnmappedMemberHandling.Skip</c> is what makes this true, and it is configured on
    /// the outbox serializer rather than per contract — so this asserts the platform setting
    /// holds for the enriched contracts specifically.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void An_older_consumer_ignores_a_field_it_does_not_know(Type contract, string property)
    {
        var enriched = JsonNode.Parse(
            Serializer().Serialize(Build(contract, withEnrichments: true)).Payload)!.AsObject();

        enriched["SomethingAddedAfterThisConsumerShipped"] = "ignored";

        var act = () => JsonSerializer.Deserialize(enriched.ToJsonString(), contract, PlainReader);

        act.Should().NotThrow(
            "{0} must tolerate a field it has never heard of, or every additive change is a " +
            "breaking one",
            contract.Name);

        contract.GetProperty(property)!.GetValue(act()).Should().Be(
            PinnedRecipient,
            "and the enrichment must still be readable alongside the unknown field");
    }

    // ── Position on the wire ─────────────────────────────────────────────

    /// <summary>
    /// <b>Every property that existed before B13.1 is still in the position it was in.</b>
    /// <c>System.Text.Json</c> writes a type's own properties in declaration order and then walks
    /// up, so declaring an enrichment in the middle of a record would silently reorder the tail
    /// of every payload that contract writes. JSON objects are unordered and no reader would
    /// break — but a golden file, a checksum or a diff of two captured payloads would, and B1.2
    /// made a specific promise about that.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void The_enrichments_are_appended_rather_than_inserted(Type contract, string property)
    {
        var names = JsonNode.Parse(
                Serializer().Serialize(Build(contract, withEnrichments: true)).Payload)!
            .AsObject()
            .Select(pair => pair.Key)
            .ToList();

        // Every enrichment any phase has appended, not only this one's.
        //
        // **PSR-1 narrowed this and the narrowing is the point.** The rule read "original" as
        // "not a B13.1 enrichment", which was the same thing as "existed before B13.1" right up
        // until a later phase appended after B13.1's — and then PSR-1's two attribute enrichments
        // counted as original, sat after EmployerUserId, and failed a contract that had not moved
        // a single property. The rule was right about what it meant and wrong about how it
        // measured it. It still fails if a B13.1 enrichment is declared before a property that
        // predates B13.1, which is the defect it exists to catch.
        var enriched = Enrichments
            .Concat(AttributeEnrichmentTests.Enrichments)
            .Concat(SeriesEnrichments)
            .Concat(ReminderEnrichments)
            .Concat(AudienceEnrichments)
            .Where(entry => entry.Contract == contract)
            .Select(entry => entry.Property)
            .ToHashSet(StringComparer.Ordinal);

        var lastOriginal = names.FindLastIndex(
            name => !enriched.Contains(name)
                && name is not (nameof(IntegrationEvent.EventId) or nameof(IntegrationEvent.OccurredAt)));

        names.IndexOf(property).Should().BeGreaterThan(
            lastOriginal,
            "{0}.{1} must come after every property the contract carried before B13.1",
            contract.Name,
            property);
    }

    /// <summary>
    /// And still before the two the base record owns, which have been the last two properties of
    /// every payload on this platform since B1.2 and are asserted byte-for-byte elsewhere.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void The_platform_identifiers_remain_the_last_two_properties(Type contract, string property)
    {
        var names = JsonNode.Parse(
                Serializer().Serialize(Build(contract, withEnrichments: true)).Payload)!
            .AsObject()
            .Select(pair => pair.Key)
            .ToList();

        names[^2].Should().Be(nameof(IntegrationEvent.EventId));
        names[^1].Should().Be(nameof(IntegrationEvent.OccurredAt));
        names.IndexOf(property).Should().BeLessThan(names.Count - 2);
    }

    // ── Coverage ─────────────────────────────────────────────────────────

    /// <summary>
    /// The inventory is complete in the other direction too: no contract in the four publishing
    /// modules carries an unlisted nullable recipient identifier. Somebody adding a fortieth
    /// enrichment without recording it here is exactly the drift the guide would then be wrong
    /// about.
    /// </summary>
    [Fact]
    public void No_enrichment_exists_outside_the_recorded_inventory()
    {
        var recorded = Enrichments
            .Concat(PredatingB131)
            .Concat(BornWithTheContract)
            .Concat(AttributionIdentifiers)
            .ToHashSet();

        var suffixed = typeof(IIntegrationEvent).Assembly.GetTypes()
            .Where(type => type.IsAssignableTo(typeof(IIntegrationEvent)) && !type.IsAbstract)
            .Where(type => type.Namespace is { } space
                && (space.StartsWith("Diaspora.Contracts.Jobs", StringComparison.Ordinal)
                    || space.StartsWith("Diaspora.Contracts.CommunityLife", StringComparison.Ordinal)
                    || space.StartsWith("Diaspora.Contracts.Guides", StringComparison.Ordinal)
                    || space.StartsWith("Diaspora.Contracts.Providers", StringComparison.Ordinal)))
            .SelectMany(
                type => type.GetProperties(),
                (type, declared) => (Contract: type, Property: declared.Name, declared.PropertyType))
            .Where(entry => entry.PropertyType == typeof(Guid?))
            .Where(entry => entry.Property.EndsWith("UserId", StringComparison.Ordinal))
            .Select(entry => (entry.Contract, entry.Property))
            .ToList();

        suffixed.Where(entry => !recorded.Contains(entry))
            .Should().BeEmpty(
                "a recipient identifier that is not in this inventory is not in the Event " +
                "Contract Enrichment Guide either");
    }

    // ── Building an instance of an arbitrary contract ────────────────────

    /// <summary>
    /// Deserializes a contract from a payload built property by property.
    /// <para>
    /// <b>Constructed through the wire rather than through an object initialiser, on purpose.</b>
    /// Every one of these records has required members; a hand-written sample per contract would
    /// be several hundred lines that stop compiling the moment somebody adds a field, and — worse
    /// — it would test the C# constructor rather than the deserializer, which is the thing every
    /// replay actually goes through.
    /// </para>
    /// <para>
    /// With <paramref name="withEnrichments"/> false, the B13.1 properties are omitted entirely,
    /// which reproduces exactly what a pre-B13.1 build wrote.
    /// </para>
    /// </summary>
    private static IIntegrationEvent Build(Type contract, bool withEnrichments)
    {
        var enriched = Enrichments
            .Where(entry => entry.Contract == contract)
            .Select(entry => entry.Property)
            .ToHashSet(StringComparer.Ordinal);

        var payload = new JsonObject();

        foreach (var declared in contract.GetProperties())
        {
            if (enriched.Contains(declared.Name))
            {
                if (withEnrichments)
                {
                    payload[declared.Name] = PinnedRecipient.ToString();
                }

                continue;
            }

            payload[declared.Name] = Sample(declared);
        }

        return (IIntegrationEvent)JsonSerializer.Deserialize(
            payload.ToJsonString(), contract, PlainReader)!;
    }

    /// <summary>A value of the right shape for one property. Content is irrelevant; presence is not.</summary>
    private static JsonNode? Sample(PropertyInfo declared)
    {
        var type = Nullable.GetUnderlyingType(declared.PropertyType) ?? declared.PropertyType;

        if (type == typeof(Guid))
        {
            return Guid.Parse("11111111-2222-3333-4444-555555555555").ToString();
        }

        if (type == typeof(string))
        {
            return "sample";
        }

        if (type == typeof(bool))
        {
            return false;
        }

        if (type == typeof(int) || type == typeof(long))
        {
            return 1;
        }

        if (type == typeof(decimal) || type == typeof(double))
        {
            return 1.5;
        }

        if (type == typeof(DateTimeOffset))
        {
            return new DateTimeOffset(2026, 3, 14, 9, 0, 0, TimeSpan.Zero)
                .ToString("O", CultureInfo.InvariantCulture);
        }

        if (type == typeof(DateOnly))
        {
            return "2026-03-14";
        }

        if (type == typeof(TimeSpan))
        {
            return "1.00:00:00";
        }

        if (type.IsEnum)
        {
            return Enum.GetNames(type)[0];
        }

        // Every collection on every one of these contracts is a list of strings. A contract that
        // grew a different one would land here and say so rather than failing obscurely.
        if (typeof(System.Collections.IEnumerable).IsAssignableFrom(type))
        {
            type.GetGenericArguments().Should().Equal(
                [typeof(string)],
                "{0} is a collection of something other than strings and this builder needs to " +
                "learn about it",
                declared.Name);

            return new JsonArray("sample");
        }

        throw new NotSupportedException(
            $"No sample value for {declared.Name} of type {declared.PropertyType}.");
    }
}
