using System.Text.Json;
using Diaspora.Contracts.Events;
using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Platform.Messaging.Outbox.Serialization;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Platform.Outbox;

/// <summary>
/// B1.2 changed the base type of every published contract in the platform. This suite is the
/// evidence that it changed nothing anyone outside the repository can observe.
/// <para>
/// <b>The risk is specific and it is not hypothetical.</b> Before B1.2, <c>EventId</c> and
/// <c>OccurredAt</c> came from <c>DomainEvent</c>, two levels above a concrete event; they now
/// come from <c>IntegrationEvent</c>, one level above it. <c>System.Text.Json</c> emits a
/// derived type's own properties first and then walks up the hierarchy, so a change in the
/// <em>depth</em> at which those two properties are declared is exactly the kind of change that
/// reorders a payload. Reordering would not break a reader — JSON objects are unordered — but it
/// would break any claim that the migration is byte-compatible, and it would break a golden-file
/// or checksum comparison somebody downstream may already depend on.
/// </para>
/// <para>
/// The literals below were captured from the pre-B1.2 build. They are not derived from the
/// current code, which is the only thing that makes them worth asserting on: a fixture generated
/// by the code under test agrees with any change that code makes.
/// </para>
/// </summary>
public sealed class MessagingSeparationCompatibilityTests
{
    private static readonly Guid PinnedEventId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid PinnedVerificationId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly Guid PinnedUserId = Guid.Parse("99999999-8888-7777-6666-555555555555");

    private static readonly DateTimeOffset PinnedOccurredAt =
        new(2026, 1, 15, 9, 30, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset PinnedApprovedAt =
        new(2026, 1, 15, 9, 29, 59, TimeSpan.Zero);

    /// <summary>
    /// Exactly what the pre-B1.2 build wrote for this event, captured before the base type
    /// changed. Note the tail: <c>EventId</c> and <c>OccurredAt</c> last, after the event's own
    /// properties.
    /// </summary>
    private const string PayloadWrittenBeforeB12 =
        """
        {"VerificationId":"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee","UserId":"99999999-8888-7777-6666-555555555555","VerificationType":"Country","TargetCountry":"GB","Partial":false,"ExpiresAt":null,"ApprovedAt":"2026-01-15T09:29:59+00:00","EventId":"11111111-2222-3333-4444-555555555555","OccurredAt":"2026-01-15T09:30:00+00:00"}
        """;

    private static VerificationApprovedIntegrationEvent PinnedEvent() =>
        new()
        {
            EventId = PinnedEventId,
            OccurredAt = PinnedOccurredAt,
            VerificationId = PinnedVerificationId,
            UserId = PinnedUserId,
            VerificationType = "Country",
            TargetCountry = "GB",
            Partial = false,
            ExpiresAt = null,
            ApprovedAt = PinnedApprovedAt
        };

    private static JsonOutboxSerializer Serializer() =>
        new(new IntegrationEventTypeResolver([typeof(IIntegrationEvent).Assembly]));

    // ── The wire format ──────────────────────────────────────────────────

    /// <summary>
    /// <b>The headline compatibility claim, and the one worth being literal about.</b> Not
    /// "deserializes equivalently" and not "has the same properties" — the same bytes, in the
    /// same order.
    /// </summary>
    [Fact]
    public void The_serialized_payload_is_byte_identical_to_the_pre_B12_form()
    {
        var envelope = Serializer().Serialize(PinnedEvent());

        envelope.Payload.Should().Be(
            PayloadWrittenBeforeB12,
            "moving EventId and OccurredAt one level down the hierarchy must not move them in " +
            "the payload");
    }

    /// <summary>
    /// A row written by the old build, read by the new one. This is the case that actually
    /// occurs during a rolling deployment: messages enqueued minutes before the release are
    /// still pending when the new instance claims them.
    /// </summary>
    [Fact]
    public void A_payload_written_before_B12_still_deserializes()
    {
        var serializer = Serializer();
        var contract = new IntegrationEventTypeResolver([typeof(IIntegrationEvent).Assembly])
            .ContractFor(typeof(VerificationApprovedIntegrationEvent));

        var replayed = serializer.Deserialize(new OutboxEnvelope
        {
            EventType = contract.Name,
            EventVersion = contract.Version,
            Payload = PayloadWrittenBeforeB12,
            EventId = PinnedEventId,
            OccurredAt = PinnedOccurredAt,
            Module = contract.Module,
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        });

        replayed.Should().BeOfType<VerificationApprovedIntegrationEvent>()
            .Which.Should().Be(PinnedEvent(), "a replay must reproduce the event exactly");

        // The identity consumers deduplicate on. A replay that minted a new one would deliver
        // every stored message a second time, to every consumer, as though it were new.
        replayed.EventId.Should().Be(PinnedEventId);
        replayed.OccurredAt.Should().Be(PinnedOccurredAt);
    }

    /// <summary>
    /// Round-tripping through the current build reproduces the same bytes, so a message enqueued
    /// today and replayed after the next release is in the same position as the one above.
    /// </summary>
    [Fact]
    public void A_round_trip_through_the_current_build_is_stable()
    {
        var serializer = Serializer();

        var once = serializer.Serialize(PinnedEvent());
        var twice = serializer.Serialize(serializer.Deserialize(once));

        twice.Payload.Should().Be(once.Payload);
        twice.EventType.Should().Be(once.EventType);
        twice.EventVersion.Should().Be(once.EventVersion);
        twice.EventId.Should().Be(once.EventId);
    }

    // ── Contract identity ────────────────────────────────────────────────

    /// <summary>
    /// The durable name is the type's full name, so B1.2 had to leave every concrete event where
    /// it was. Changing the base type is invisible to the name; moving a namespace would not be,
    /// and would strand every stored row of that contract in the dead-letter table.
    /// </summary>
    [Fact]
    public void No_contract_name_or_version_moved()
    {
        var resolver = new IntegrationEventTypeResolver([typeof(IIntegrationEvent).Assembly]);

        resolver.ContractFor(typeof(VerificationApprovedIntegrationEvent))
            .Name.Should().Be(
                "Diaspora.Contracts.Trust.V1.Events.VerificationApprovedIntegrationEvent");

        resolver.Contracts.Should().OnlyContain(
            contract => contract.Name.StartsWith("Diaspora.Contracts.", StringComparison.Ordinal)
                        || contract.Name.StartsWith("diaspora.", StringComparison.Ordinal),
            "a contract name that changed shape in this phase would be a silent break");

        resolver.Contracts.Should().OnlyContain(
            contract => contract.Version >= 1,
            "B1.2 is a source-level change and versions no contract forward");
    }

    /// <summary>
    /// Every shipped contract still round-trips, not just the one pinned above. A base-type
    /// change is uniform, so a failure here would mean something else moved with it.
    /// </summary>
    [Fact]
    public void Every_shipped_contract_still_round_trips()
    {
        var serializer = Serializer();

        var declared = typeof(IIntegrationEvent).Assembly.GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Where(type => typeof(IIntegrationEvent).IsAssignableFrom(type))
            .ToArray();

        declared.Should().NotBeEmpty();

        foreach (var type in declared)
        {
            // Constructed uninitialized on purpose: every contract uses `required` members, and
            // the point here is the shape of the payload rather than any particular value.
            var instance = (IIntegrationEvent)System.Runtime.CompilerServices
                .RuntimeHelpers.GetUninitializedObject(type);

            var envelope = serializer.Serialize(instance);

            envelope.Payload.Should().Contain(
                "\"EventId\"", $"{type.Name} must still carry its identity in the payload");
            envelope.Payload.Should().Contain("\"OccurredAt\"");

            serializer.Deserialize(envelope).Should().BeOfType(type);
        }
    }

    /// <summary>
    /// The property order within the payload, stated as a rule rather than as one example: an
    /// event's own fields come first and the two inherited ones come last. This is what would
    /// have changed had <c>IntegrationEvent</c> been given a base class, and it is the assertion
    /// that would catch somebody adding one.
    /// </summary>
    [Fact]
    public void The_inherited_properties_are_written_last_for_every_contract()
    {
        var serializer = Serializer();

        var declared = typeof(IIntegrationEvent).Assembly.GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Where(type => typeof(IIntegrationEvent).IsAssignableFrom(type))
            .Where(type => type.GetProperties()
                .Any(property => property.Name is not ("EventId" or "OccurredAt")))
            .ToArray();

        declared.Should().NotBeEmpty();

        foreach (var type in declared)
        {
            var instance = (IIntegrationEvent)System.Runtime.CompilerServices
                .RuntimeHelpers.GetUninitializedObject(type);

            var names = JsonDocument.Parse(serializer.Serialize(instance).Payload)
                .RootElement.EnumerateObject()
                .Select(property => property.Name)
                .ToArray();

            names[^2..].Should().Equal(
                ["EventId", "OccurredAt"],
                $"{type.Name} must keep the pre-B1.2 property order");
        }
    }
}
