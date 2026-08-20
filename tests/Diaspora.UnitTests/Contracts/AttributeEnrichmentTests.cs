using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diaspora.Contracts.Events;
using Diaspora.Contracts.Jobs.V1.Events;
using Diaspora.Contracts.Providers.V1.Events;
using Diaspora.Platform.Messaging.Outbox.Serialization;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Contracts;

/// <summary>
/// PSR-1's attribute enrichments, held to the same contract B13.1 held its recipient enrichments to.
/// <para>
/// <b>A separate inventory, deliberately.</b> <c>EventContractEnrichmentTests</c> asserts every
/// entry is a <c>Guid?</c>, because every one of B13.1's thirty-nine was a member identity and the
/// rule that made that phase safe was "a contract that cannot express 'nobody' forces a caller to
/// invent an identifier". These three are not identities — they are attributes of a public thing: a
/// category key and a country code. Widening that rule to admit a string would have weakened the
/// guard for the case it was written for, which is the mistake the platform already declined to
/// make when a name-based feature-gate guard fired on an honest name.
/// </para>
/// <para>
/// Everything else about the two rules is the same, because the compatibility claim is the same:
/// nullable, never <c>required</c>, init-only, appended after every existing property, written to
/// the wire, and absent from a historical payload without complaint.
/// </para>
/// </summary>
public class AttributeEnrichmentTests
{
    /// <summary>
    /// The enrichments PSR-1 added, and why each one exists.
    /// <para>
    /// <b>Three, where the brief listed a dozen candidates.</b> DEBT-006 offered employment type,
    /// seniority, location and remote status as possibilities; none is here, because nothing
    /// consumes them. What is here is what B14 demonstrably reads — a key to match an interest
    /// against, and a country to scope it by — which is the shape Marketplace's sale contract has
    /// published since B10. A contract carrying fields no consumer wants is a contract nobody can
    /// safely change later.
    /// </para>
    /// </summary>
    public static readonly (Type Contract, string Property)[] Enrichments =
    [
        // ── DEBT-005: the provider's category, as a key rather than only an identifier ───────
        (typeof(ProviderPublishedIntegrationEvent), "CategoryKey"),

        // ── DEBT-006: what the job was about, on the application that was made to it ─────────
        (typeof(ApplicationSubmittedIntegrationEvent), "CategoryKey"),
        (typeof(ApplicationSubmittedIntegrationEvent), "CountryCode")
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

    private const string PinnedValue = "plumbing";

    private static readonly JsonSerializerOptions PlainReader =
        new() { PropertyNameCaseInsensitive = true };

    private static JsonOutboxSerializer Serializer() =>
        new(new IntegrationEventTypeResolver([typeof(IIntegrationEvent).Assembly]));

    // ── The shape of an enrichment ───────────────────────────────────────

    /// <summary>
    /// Nullable and never <c>required</c>. <c>System.Text.Json</c> honours C#'s <c>required</c>, so
    /// an enrichment declared that way would throw on every payload written before it existed.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void Every_attribute_enrichment_is_an_optional_nullable_string(
        Type contract, string property)
    {
        var declared = contract.GetProperty(property);

        declared.Should().NotBeNull("{0} declares {1}", contract.Name, property);

        declared!.PropertyType.Should().Be<string>(
            "{0}.{1} carries a key or a code, not an identifier", contract.Name, property);

        new NullabilityInfoContext().Create(declared).WriteState
            .Should().Be(
                NullabilityState.Nullable,
                "{0}.{1} must be able to say 'this publisher did not know', or a relay that " +
                "cannot answer will invent a value — which is the placeholder defect B13.1 fixed " +
                "in three contracts at once",
                contract.Name,
                property);

        declared.GetCustomAttributes()
            .Should().NotContain(
                attribute => attribute.GetType().Name == "RequiredMemberAttribute",
                "{0}.{1} must not be required, or every payload written before PSR-1 fails to " +
                "deserialize",
                contract.Name,
                property);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Every_attribute_enrichment_is_init_only(Type contract, string property)
    {
        var setter = contract.GetProperty(property)!.SetMethod;

        setter.Should().NotBeNull();

        setter!.ReturnParameter.GetRequiredCustomModifiers()
            .Should().Contain(
                type => type.Name == "IsExternalInit",
                "{0}.{1} must be init-only, like every other property on a published contract",
                contract.Name,
                property);
    }

    // ── Position on the wire ─────────────────────────────────────────────

    /// <summary>
    /// <b>Appended, never inserted.</b> <c>System.Text.Json</c> writes a type's properties in
    /// declaration order, so declaring an enrichment in the middle of a record silently reorders
    /// the tail of every payload that contract writes. No reader breaks — JSON objects are
    /// unordered — but a golden file, a checksum or a diff of two captured payloads does, and B1.2
    /// made a specific promise about that.
    /// </summary>
    [Fact]
    public void Every_attribute_enrichment_is_declared_last()
    {
        foreach (var contract in Enrichments.Select(entry => entry.Contract).Distinct())
        {
            var declared = contract
                .GetProperties(
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(property => property.Name)
                .ToList();

            var enriched = Enrichments
                .Where(entry => entry.Contract == contract)
                .Select(entry => entry.Property)
                .ToHashSet(StringComparer.Ordinal);

            declared.Skip(declared.FindIndex(enriched.Contains))
                .Should().OnlyContain(
                    name => enriched.Contains(name) || IsRecipientEnrichment(contract, name),
                    "{0} must declare its enrichments after every property that existed before " +
                    "them; B13.1's recipient enrichments were there first and are allowed among " +
                    "them",
                    contract.Name);
        }
    }

    // ── Serialization and compatibility ──────────────────────────────────

    /// <summary>
    /// Set on construction, present on the wire. A property that can be set but is not serialized
    /// is a field the publisher believes it sent.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void An_attribute_enrichment_reaches_the_payload(Type contract, string property)
    {
        var integrationEvent = Build(contract, property, PinnedValue);

        var payload = JsonNode.Parse(Serializer().Serialize(integrationEvent).Payload)!.AsObject();

        payload.ContainsKey(property).Should().BeTrue(
            "{0} must write {1} to the wire, not merely hold it", contract.Name, property);

        payload[property]!.GetValue<string>().Should().Be(PinnedValue);
    }

    /// <summary>A full round trip through the real outbox serializer.</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void An_attribute_enrichment_survives_a_round_trip(Type contract, string property)
    {
        var serializer = Serializer();
        var original = Build(contract, property, PinnedValue);

        var restored = serializer.Deserialize(serializer.Serialize(original));

        restored.Should().BeOfType(contract);
        contract.GetProperty(property)!.GetValue(restored).Should().Be(PinnedValue);
    }

    /// <summary>
    /// <b>The compatibility claim, tested against the thing it is a claim about.</b> The payload is
    /// built without the enrichment — the exact bytes a pre-PSR-1 build wrote — and must read as
    /// null rather than as an empty string a consumer could mistake for a category nobody named.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void A_payload_written_before_the_enrichment_still_deserializes(
        Type contract, string property)
    {
        var serializer = Serializer();
        var historical = Build(contract, property, value: null);

        var restored = serializer.Deserialize(serializer.Serialize(historical));

        contract.GetProperty(property)!.GetValue(restored)
            .Should().BeNull(
                "a replayed payload that never carried {0} must read as absent, not as a key a " +
                "consumer would try to match an interest against",
                property);
    }

    /// <summary>
    /// The other direction: a consumer running pre-PSR-1 code meets a payload that gained a field.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void An_older_consumer_ignores_a_field_it_does_not_know(Type contract, string property)
    {
        var enriched = JsonNode.Parse(
            Serializer().Serialize(Build(contract, property, PinnedValue)).Payload)!.AsObject();

        enriched["SomethingAddedAfterThisConsumerShipped"] = "ignored";

        var act = () => JsonSerializer.Deserialize(enriched.ToJsonString(), contract, PlainReader);

        act.Should().NotThrow(
            "{0} must tolerate a field it has never heard of, or every additive change is a " +
            "breaking one",
            contract.Name);

        contract.GetProperty(property)!.GetValue(act()).Should().Be(PinnedValue);
    }

    // ── The inventory is the whole set ───────────────────────────────────

    /// <summary>
    /// <b>The rule that keeps this file honest.</b> Every nullable string on the two enriched
    /// contracts that is not a property they shipped with must be listed above — otherwise a
    /// fourth enrichment added later is exempt from every rule in this file, silently, which is
    /// the shape of finding the platform has already paid for twice.
    /// </summary>
    [Fact]
    public void The_inventory_names_every_attribute_enrichment_on_the_contracts_it_covers()
    {
        string[] shippedBefore =
        [
            "ProviderType", "Name", "Visibility", "CountryCode", "Reason", "Message"
        ];

        var unlisted = Enrichments
            .Select(entry => entry.Contract)
            .Distinct()
            .SelectMany(contract => contract
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(property =>
                    property.PropertyType == typeof(string) &&
                    new NullabilityInfoContext().Create(property).WriteState == NullabilityState.Nullable &&
                    !shippedBefore.Contains(property.Name, StringComparer.Ordinal) &&
                    !Enrichments.Contains((contract, property.Name)))
                .Select(property => $"{contract.Name}.{property.Name}"))
            .ToList();

        unlisted.Should().BeEmpty(
            "an enrichment absent from the inventory is exempt from every rule in this file: " +
            string.Join(", ", unlisted));
    }

    /// <summary>
    /// B13.1's recipient enrichments are also declared at the end of these records, and were there
    /// first. The position rule has to allow them without listing them twice.
    /// </summary>
    private static bool IsRecipientEnrichment(Type contract, string property) =>
        EventContractEnrichmentTests.Enrichments.Contains((contract, property));

    /// <summary>
    /// A minimally valid instance of <paramref name="contract"/> with one property set to
    /// <paramref name="value"/> — or left null, which is what a pre-enrichment payload looks like.
    /// <para>
    /// Built by reflection over the required members rather than by hand, so a contract that gains
    /// a required property does not quietly stop being covered here.
    /// </para>
    /// </summary>
    private static IIntegrationEvent Build(Type contract, string property, string? value)
    {
        var instance = (IIntegrationEvent)System.Runtime.CompilerServices.RuntimeHelpers
            .GetUninitializedObject(contract);

        foreach (var declared in contract.GetProperties(
            BindingFlags.Public | BindingFlags.Instance))
        {
            if (declared.SetMethod is null)
            {
                continue;
            }

            var target = declared.Name == property
                ? value
                : DefaultFor(declared.PropertyType);

            if (target is not null || declared.Name == property)
            {
                declared.SetValue(instance, target);
            }
        }

        return instance;
    }

    /// <summary>
    /// A harmless value for every required property, so the instance serializes. Only the property
    /// under test carries anything a rule reads.
    /// </summary>
    private static object? DefaultFor(Type type) =>
        type == typeof(string) ? "x"
        : type == typeof(Guid) ? Guid.NewGuid()
        : type == typeof(DateTimeOffset) ? DateTimeOffset.UnixEpoch
        : type == typeof(int) ? 0
        : type == typeof(bool) ? false
        : null;
}
