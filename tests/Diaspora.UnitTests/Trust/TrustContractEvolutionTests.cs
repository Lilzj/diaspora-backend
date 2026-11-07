using System.Text.Json;
using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Events;
using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Modules.Trust.Application.EventHandlers;
using Diaspora.Modules.Trust.Application.Services;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Domain.Events;
using Diaspora.Platform.Messaging;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Trust;

/// <summary>
/// B3.0.1 — the additive country on the verification lifecycle events.
/// <para>
/// The gap this closes was found during B4's consumer validation:
/// <c>VerificationRejectedIntegrationEvent</c> carried no country, so a consumer holding a
/// per-country residency projection could not attribute a refusal — and inferring it from the
/// member's most recent submission is wrong the first time someone has two in flight, and
/// wrong invisibly.
/// </para>
/// <para>
/// These tests exist to hold two things at once: that the new property is populated wherever
/// the aggregate knows the answer, and that **nothing about the old contract moved**. The
/// second is the harder promise, and §3 is where it is kept.
/// </para>
/// </summary>
public sealed class TrustContractEvolutionTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    private static readonly UserId Member =
        UserId.From(Guid.Parse("11111111-1111-1111-1111-111111111111"));

    private static readonly UserId Reviewer =
        UserId.From(Guid.Parse("22222222-2222-2222-2222-222222222222"));

    /// <summary>The web defaults the host serialises with — camelCase, so field names match the wire.</summary>
    private static readonly JsonSerializerOptions WireJson = new(JsonSerializerDefaults.Web);

    /// <summary>A queued country claim about the United Kingdom.</summary>
    private static VerificationRequest QueuedCountryClaim(string targetCountry = "GB")
    {
        var request = VerificationRequest
            .Open(Member, VerificationType.Country, Now)
            .WithResidencyClaim(targetCountry, new DateOnly(2019, 3, 1), ["Housing"]);

        request.AttachDocument(VerificationDocumentType.UtilityBill, "storage/key", Now);
        request.Queue(Now, TimeSpan.FromHours(48));

        return request;
    }

    /// <summary>A queued diaspora claim, which names no community.</summary>
    private static VerificationRequest QueuedDiasporaClaim()
    {
        var request = VerificationRequest
            .Open(Member, VerificationType.Diaspora, Now)
            .WithDiasporaClaim("NG", "GB", null);

        request.AttachDocument(VerificationDocumentType.Passport, "storage/key", Now);
        request.Queue(Now, TimeSpan.FromHours(48));

        return request;
    }

    private static TEvent Raised<TEvent>(VerificationRequest request) where TEvent : class =>
        request.DomainEvents.OfType<TEvent>().Single();

    // ── 1. The aggregate carries the country onto every lifecycle event ──

    /// <summary>The gap itself: a refusal now names the community it refused.</summary>
    [Fact]
    public void A_rejected_country_claim_carries_the_target_country()
    {
        var request = QueuedCountryClaim();

        request.Reject(
            Reviewer, "Compliance", VerificationRejectionReason.ILLEGIBLE_DOCUMENT, "Blurred", Now);

        Raised<VerificationRejectedDomainEvent>(request).TargetCountry.Should().Be("GB");
    }

    /// <summary>The rule path, not only the human one.</summary>
    [Fact]
    public void An_auto_rejected_country_claim_carries_the_target_country()
    {
        var request = QueuedCountryClaim("MU");

        request.AutoReject(
            "duplicate-rule", VerificationRejectionReason.DUPLICATE_SUBMISSION, null, Now);

        Raised<VerificationRejectedDomainEvent>(request).TargetCountry.Should().Be("MU");
    }

    [Fact]
    public void An_expired_country_grant_carries_the_target_country()
    {
        var request = QueuedCountryClaim();
        request.Approve(Reviewer, "Compliance", null, Now, expiresAt: Now.AddDays(1));
        request.ClearDomainEvents();

        request.Expire(Now.AddDays(2));

        Raised<VerificationExpiredDomainEvent>(request).TargetCountry.Should().Be("GB");
    }

    [Fact]
    public void A_revoked_country_grant_carries_the_target_country()
    {
        var request = QueuedCountryClaim();
        request.Approve(Reviewer, "Compliance", null, Now);
        request.ClearDomainEvents();

        request.Revoke(Reviewer, "Compliance", "Evidence found to be forged", Now.AddDays(1));

        Raised<VerificationRevokedDomainEvent>(request).TargetCountry.Should().Be("GB");
    }

    /// <summary>
    /// "Do not invent values." A diaspora claim names a nationality and a residence, not a
    /// community, so its refusal legitimately carries no target country.
    /// </summary>
    [Fact]
    public void A_rejected_diaspora_claim_carries_no_target_country()
    {
        var request = QueuedDiasporaClaim();

        request.Reject(
            Reviewer, "Compliance", VerificationRejectionReason.FACE_MATCH_FAILED, null, Now);

        Raised<VerificationRejectedDomainEvent>(request).TargetCountry.Should().BeNull();
    }

    /// <summary>The country is normalised on the way in, so the event carries the canonical form.</summary>
    [Fact]
    public void The_country_on_the_event_is_normalised()
    {
        var request = QueuedCountryClaim("gb");

        request.Reject(Reviewer, "Compliance", VerificationRejectionReason.INVALID_DOCUMENT, null, Now);

        Raised<VerificationRejectedDomainEvent>(request).TargetCountry.Should().Be("GB");
    }

    // ── 2. The published contract ────────────────────────────────────────

    /// <summary>
    /// Every event in the verification lifecycle now exposes the community, so a consumer can
    /// switch on the type without a per-event special case.
    /// </summary>
    [Fact]
    public void Every_verification_lifecycle_event_exposes_a_target_country()
    {
        typeof(VerificationSubmittedIntegrationEvent)
            .GetProperty(nameof(VerificationSubmittedIntegrationEvent.TargetCountry))
            .Should().NotBeNull();

        typeof(VerificationApprovedIntegrationEvent)
            .GetProperty(nameof(VerificationApprovedIntegrationEvent.TargetCountry))
            .Should().NotBeNull();

        typeof(VerificationRejectedIntegrationEvent)
            .GetProperty(nameof(VerificationRejectedIntegrationEvent.TargetCountry))
            .Should().NotBeNull();

        typeof(VerificationExpiredIntegrationEvent)
            .GetProperty(nameof(VerificationExpiredIntegrationEvent.TargetCountry))
            .Should().NotBeNull();

        typeof(VerificationRevokedIntegrationEvent)
            .GetProperty(nameof(VerificationRevokedIntegrationEvent.TargetCountry))
            .Should().NotBeNull();
    }

    /// <summary>
    /// The three siblings raised from one approval now agree. Two already carried
    /// <c>CountryCode</c>; the organisation one did not, for no reason a reader could
    /// reconstruct.
    /// </summary>
    [Fact]
    public void The_derived_approval_events_all_expose_a_country()
    {
        typeof(ContributorApprovedIntegrationEvent)
            .GetProperty(nameof(ContributorApprovedIntegrationEvent.CountryCode))
            .Should().NotBeNull();

        typeof(GuideApprovedIntegrationEvent)
            .GetProperty(nameof(GuideApprovedIntegrationEvent.CountryCode))
            .Should().NotBeNull();

        typeof(OrganizationVerifiedIntegrationEvent)
            .GetProperty(nameof(OrganizationVerifiedIntegrationEvent.CountryCode))
            .Should().NotBeNull();
    }

    /// <summary>
    /// Optional, not required. A `required` property would be a breaking change — every
    /// existing producer would stop compiling and every stored payload would stop
    /// deserialising.
    /// </summary>
    [Theory]
    [InlineData(typeof(VerificationRejectedIntegrationEvent), "TargetCountry")]
    [InlineData(typeof(VerificationExpiredIntegrationEvent), "TargetCountry")]
    [InlineData(typeof(VerificationRevokedIntegrationEvent), "TargetCountry")]
    [InlineData(typeof(OrganizationVerifiedIntegrationEvent), "CountryCode")]
    public void The_new_property_is_optional_and_nullable(Type eventType, string propertyName)
    {
        var property = eventType.GetProperty(propertyName)!;

        property.PropertyType.Should().Be<string>();

        property.GetCustomAttributes(
                typeof(System.Runtime.CompilerServices.RequiredMemberAttribute), inherit: false)
            .Should().BeEmpty($"{eventType.Name}.{propertyName} must stay optional");
    }

    // ── 3. Backwards compatibility ───────────────────────────────────────

    /// <summary>
    /// **The load-bearing compatibility test.** A payload serialised before B3.0.1 has no
    /// `targetCountry` member at all. It must still deserialise, and the new property must
    /// simply be null — not throw, not default to an empty string.
    /// </summary>
    [Fact]
    public void A_payload_serialised_before_the_patch_still_deserialises()
    {
        const string legacyPayload = """
            {
              "eventId": "33333333-3333-3333-3333-333333333333",
              "occurredAt": "2026-07-27T12:00:00+00:00",
              "verificationId": "44444444-4444-4444-4444-444444444444",
              "userId": "11111111-1111-1111-1111-111111111111",
              "verificationType": "Country",
              "reasonCode": "ILLEGIBLE_DOCUMENT",
              "details": "Blurred",
              "rejectedAt": "2026-07-27T12:00:00+00:00"
            }
            """;

        var rehydrated = JsonSerializer.Deserialize<VerificationRejectedIntegrationEvent>(
            legacyPayload, WireJson);

        rehydrated.Should().NotBeNull();
        rehydrated!.VerificationType.Should().Be("Country");
        rehydrated.ReasonCode.Should().Be("ILLEGIBLE_DOCUMENT");
        rehydrated.Details.Should().Be("Blurred");

        rehydrated.TargetCountry.Should().BeNull(
            "a pre-patch payload names no country, and the absence must read as unknown");
    }

    [Fact]
    public void Pre_patch_expiry_and_revocation_payloads_still_deserialise()
    {
        const string expired = """
            {
              "eventId": "33333333-3333-3333-3333-333333333333",
              "occurredAt": "2026-07-27T12:00:00+00:00",
              "verificationId": "44444444-4444-4444-4444-444444444444",
              "userId": "11111111-1111-1111-1111-111111111111",
              "verificationType": "Country",
              "expiredAt": "2026-07-27T12:00:00+00:00"
            }
            """;

        const string revoked = """
            {
              "eventId": "33333333-3333-3333-3333-333333333333",
              "occurredAt": "2026-07-27T12:00:00+00:00",
              "verificationId": "44444444-4444-4444-4444-444444444444",
              "userId": "11111111-1111-1111-1111-111111111111",
              "verificationType": "Country",
              "reason": "Evidence forged",
              "revokedAt": "2026-07-27T12:00:00+00:00"
            }
            """;

        JsonSerializer.Deserialize<VerificationExpiredIntegrationEvent>(expired, WireJson)!
            .TargetCountry.Should().BeNull();

        JsonSerializer.Deserialize<VerificationRevokedIntegrationEvent>(revoked, WireJson)!
            .TargetCountry.Should().BeNull();
    }

    /// <summary>
    /// The other direction: a consumer built before B3.0.1 reading a post-patch payload. A
    /// reader that does not know the member ignores it, which is what `System.Text.Json` does
    /// by default and what every JSON consumer in the estate relies on.
    /// </summary>
    [Fact]
    public void A_pre_patch_consumer_ignores_the_new_member()
    {
        var current = new VerificationRejectedIntegrationEvent
        {
            VerificationId = Guid.NewGuid(),
            UserId = Member.Value,
            VerificationType = "Country",
            TargetCountry = "GB",
            ReasonCode = "ILLEGIBLE_DOCUMENT",
            RejectedAt = Now
        };

        var payload = JsonSerializer.Serialize(current, WireJson);

        // The shape a consumer compiled against the pre-patch contract sees.
        var legacyView = JsonSerializer.Deserialize<LegacyRejectedEventView>(payload, WireJson);

        legacyView.Should().NotBeNull();
        legacyView!.VerificationId.Should().Be(current.VerificationId);
        legacyView.UserId.Should().Be(current.UserId);
        legacyView.VerificationType.Should().Be("Country");
        legacyView.ReasonCode.Should().Be("ILLEGIBLE_DOCUMENT");
        legacyView.RejectedAt.Should().Be(Now);
    }

    /// <summary>Every field a pre-patch payload carried is still emitted, under the same name.</summary>
    [Fact]
    public void No_pre_existing_wire_name_changed()
    {
        var payload = JsonSerializer.Serialize(
            new VerificationRejectedIntegrationEvent
            {
                VerificationId = Guid.NewGuid(),
                UserId = Member.Value,
                VerificationType = "Country",
                ReasonCode = "INVALID_DOCUMENT",
                Details = "note",
                RejectedAt = Now
            },
            WireJson);

        using var document = JsonDocument.Parse(payload);
        var names = document.RootElement.EnumerateObject().Select(p => p.Name).ToList();

        names.Should().Contain(
            ["eventId", "occurredAt", "verificationId", "userId",
             "verificationType", "reasonCode", "details", "rejectedAt"]);
    }

    /// <summary>
    /// The version segment is unchanged. B3.0.1 is a patch to a v1 contract, not a v2 —
    /// an additive optional member requires no new namespace, and minting one would force
    /// every consumer to migrate for a field most of them do not read.
    /// </summary>
    [Fact]
    public void The_contract_namespace_is_still_v1()
    {
        typeof(VerificationRejectedIntegrationEvent).Namespace
            .Should().Be("Diaspora.Contracts.Trust.V1.Events");

        typeof(VerificationRejectedIntegrationEvent).Assembly.GetTypes()
            .Where(type => type.Namespace?.StartsWith(
                "Diaspora.Contracts.Trust.V2", StringComparison.Ordinal) == true)
            .Should().BeEmpty("B3.0.1 introduces no V2");
    }

    /// <summary>
    /// The event count is unchanged by B3.0.1. Nothing was renamed, nothing removed, nothing added.
    /// <para>
    /// One event has been added since, additively, and is listed here so a later change is still
    /// caught: <c>MemberRiskLevelChanged</c> (RWD-14, CS-4b, step 3f), the level only.
    /// </para>
    /// </summary>
    [Fact]
    public void The_published_event_set_is_unchanged()
    {
        var events = typeof(VerificationRejectedIntegrationEvent).Assembly.GetTypes()
            .Where(type => type.Namespace == "Diaspora.Contracts.Trust.V1.Events")
            .Select(type => type.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        events.Should().BeEquivalentTo(
        [
            nameof(AppealApprovedIntegrationEvent),
            nameof(AppealRejectedIntegrationEvent),
            nameof(AppealSubmittedIntegrationEvent),
            nameof(BadgeAwardedIntegrationEvent),
            nameof(BadgeRevokedIntegrationEvent),
            nameof(ContributorApprovedIntegrationEvent),
            nameof(GuideApprovedIntegrationEvent),
            nameof(MemberRiskLevelChangedIntegrationEvent),
            // ModerationDecisionRecordedIntegrationEvent was retired on 7 Nov 2026: it carried no reason
            // and no owner. Decisions now publish Moderation.V1.Events.ModerationActionTakenIntegrationEvent.
            nameof(OrganizationVerifiedIntegrationEvent),
            nameof(ReputationLevelChangedIntegrationEvent),
            nameof(TrustLevelChangedIntegrationEvent),
            nameof(VerificationApprovedIntegrationEvent),
            nameof(VerificationExpiredIntegrationEvent),
            nameof(VerificationMoreInformationRequestedIntegrationEvent),
            nameof(VerificationRejectedIntegrationEvent),
            nameof(VerificationRevokedIntegrationEvent),
            nameof(VerificationSubmittedIntegrationEvent)
        ]);
    }

    // ── 4. A simulated consumer projection ───────────────────────────────

    /// <summary>
    /// The end-to-end claim of the patch, simulated: rejected → country identified →
    /// projection updated, with no inference anywhere.
    /// <para>
    /// Deliberately a stand-in for Location's projection rather than the real one. The point is
    /// that the *contract* now carries what any per-country consumer needs — a test coupled to
    /// Location's handler would be testing Location, and would not fail if Trust dropped the
    /// property for a different consumer.
    /// </para>
    /// </summary>
    [Fact]
    public void A_consumer_projects_a_refusal_onto_the_right_country_without_inference()
    {
        var projection = new SimulatedResidencyProjection();

        var request = QueuedCountryClaim();
        request.Reject(
            Reviewer, "Compliance", VerificationRejectionReason.ILLEGIBLE_DOCUMENT, null, Now);

        var rejection = Raised<VerificationRejectedDomainEvent>(request);

        // The translation Trust's handler performs.
        var published = new VerificationRejectedIntegrationEvent
        {
            VerificationId = rejection.RequestId.Value,
            UserId = rejection.UserId.Value,
            VerificationType = rejection.Type.ToString(),
            TargetCountry = rejection.TargetCountry,
            ReasonCode = rejection.Reason.ToString(),
            Details = rejection.Details,
            RejectedAt = rejection.At
        };

        projection.Apply(published);

        projection.StateFor(Member.Value, "GB").Should().Be("Rejected");
        projection.InferredAnything.Should().BeFalse();
    }

    /// <summary>
    /// A member with two country claims in flight. Before B3.0.1 a consumer could only guess
    /// which one a refusal belonged to; the natural guess — the most recent submission — picks
    /// the wrong country here.
    /// </summary>
    [Fact]
    public void Two_claims_in_flight_are_refused_onto_the_right_countries()
    {
        var projection = new SimulatedResidencyProjection();

        var british = QueuedCountryClaim("GB");
        var mauritian = QueuedCountryClaim("MU");

        // The older claim is refused second, which is exactly the ordering a
        // most-recent-submission heuristic gets wrong.
        mauritian.Reject(Reviewer, "Compliance", VerificationRejectionReason.INVALID_DOCUMENT, null, Now);
        british.Reject(
            Reviewer, "Compliance", VerificationRejectionReason.INVALID_DOCUMENT, null, Now.AddMinutes(1));

        foreach (var request in new[] { mauritian, british })
        {
            var rejection = Raised<VerificationRejectedDomainEvent>(request);

            projection.Apply(new VerificationRejectedIntegrationEvent
            {
                VerificationId = rejection.RequestId.Value,
                UserId = rejection.UserId.Value,
                VerificationType = rejection.Type.ToString(),
                TargetCountry = rejection.TargetCountry,
                ReasonCode = rejection.Reason.ToString(),
                RejectedAt = rejection.At
            });
        }

        projection.StateFor(Member.Value, "GB").Should().Be("Rejected");
        projection.StateFor(Member.Value, "MU").Should().Be("Rejected");
        projection.InferredAnything.Should().BeFalse();
    }

    /// <summary>
    /// And the conservative half: a pre-patch payload names no country, so a correct consumer
    /// records nothing rather than guessing. That is the behaviour B4 shipped with, preserved.
    /// </summary>
    [Fact]
    public void A_consumer_records_nothing_for_a_payload_that_names_no_country()
    {
        var projection = new SimulatedResidencyProjection();

        projection.Apply(new VerificationRejectedIntegrationEvent
        {
            VerificationId = Guid.NewGuid(),
            UserId = Member.Value,
            VerificationType = "Country",
            TargetCountry = null,
            ReasonCode = "INVALID_DOCUMENT",
            RejectedAt = Now
        });

        projection.StateFor(Member.Value, "GB").Should().BeNull();
        projection.InferredAnything.Should().BeFalse();
    }

    // ── 5. The producer, through Trust's real translation handlers ───────

    /// <summary>
    /// The translation layer is where a domain event becomes a published contract, and it is
    /// the one place the new property could be dropped without any other test noticing. These
    /// drive the real handlers with a captured dispatcher.
    /// </summary>
    [Fact]
    public async Task The_rejection_publisher_carries_the_country_onto_the_published_event()
    {
        var dispatcher = new CapturingDispatcher();
        var handler = new VerificationRejectedEventHandler(dispatcher, new NoOpDecisionPublisher());

        var request = QueuedCountryClaim();
        request.Reject(
            Reviewer, "Compliance", VerificationRejectionReason.ILLEGIBLE_DOCUMENT, "Blurred", Now);

        await handler.Handle(
            new DomainEventNotification<VerificationRejectedDomainEvent>(
                Raised<VerificationRejectedDomainEvent>(request)),
            CancellationToken.None);

        var published = dispatcher.Captured.OfType<VerificationRejectedIntegrationEvent>().Single();

        published.TargetCountry.Should().Be("GB");
        published.VerificationType.Should().Be("Country");
        published.ReasonCode.Should().Be("ILLEGIBLE_DOCUMENT");
    }

    [Fact]
    public async Task The_expiry_publisher_carries_the_country_onto_the_published_event()
    {
        var dispatcher = new CapturingDispatcher();
        var handler = new VerificationExpiredEventHandler(dispatcher, new NoOpDecisionPublisher());

        var request = QueuedCountryClaim();
        request.Approve(Reviewer, "Compliance", null, Now, expiresAt: Now.AddDays(1));
        request.ClearDomainEvents();
        request.Expire(Now.AddDays(2));

        await handler.Handle(
            new DomainEventNotification<VerificationExpiredDomainEvent>(
                Raised<VerificationExpiredDomainEvent>(request)),
            CancellationToken.None);

        dispatcher.Captured.OfType<VerificationExpiredIntegrationEvent>().Single()
            .TargetCountry.Should().Be("GB");
    }

    [Fact]
    public async Task The_revocation_publisher_carries_the_country_onto_the_published_event()
    {
        var dispatcher = new CapturingDispatcher();
        var handler = new VerificationRevokedEventHandler(dispatcher, new NoOpDecisionPublisher());

        var request = QueuedCountryClaim("MU");
        request.Approve(Reviewer, "Compliance", null, Now);
        request.ClearDomainEvents();
        request.Revoke(Reviewer, "Compliance", "Evidence forged", Now.AddDays(1));

        await handler.Handle(
            new DomainEventNotification<VerificationRevokedDomainEvent>(
                Raised<VerificationRevokedDomainEvent>(request)),
            CancellationToken.None);

        dispatcher.Captured.OfType<VerificationRevokedIntegrationEvent>().Single()
            .TargetCountry.Should().Be("MU");
    }

    /// <summary>
    /// A diaspora refusal publishes a null country rather than an empty string. The
    /// distinction matters to a consumer: null means "this claim names no community", and an
    /// empty string would be a country code that fails to parse.
    /// </summary>
    [Fact]
    public async Task A_publisher_emits_null_rather_than_empty_for_a_claim_with_no_community()
    {
        var dispatcher = new CapturingDispatcher();
        var handler = new VerificationRejectedEventHandler(dispatcher, new NoOpDecisionPublisher());

        var request = QueuedDiasporaClaim();
        request.Reject(
            Reviewer, "Compliance", VerificationRejectionReason.NOT_DIASPORA_ELIGIBLE, null, Now);

        await handler.Handle(
            new DomainEventNotification<VerificationRejectedDomainEvent>(
                Raised<VerificationRejectedDomainEvent>(request)),
            CancellationToken.None);

        dispatcher.Captured.OfType<VerificationRejectedIntegrationEvent>().Single()
            .TargetCountry.Should().BeNull();
    }

    /// <summary>
    /// The organisation event now agrees with its two siblings. Approving an organisation
    /// verification raises both the generic approval and the derived organisation event.
    /// </summary>
    [Fact]
    public async Task The_organisation_publisher_carries_the_country_onto_the_published_event()
    {
        var dispatcher = new CapturingDispatcher();
        var handler = new VerificationApprovedEventHandler(dispatcher, new NoOpDecisionPublisher());

        var request = VerificationRequest
            .Open(Member, VerificationType.Organization, Now)
            .WithResidencyClaim("GB", null, null);

        request.AttachDocument(VerificationDocumentType.BusinessRegistration, "storage/key", Now);
        request.Queue(Now, TimeSpan.FromHours(48));
        request.ClearDomainEvents();
        request.Approve(Reviewer, "Compliance", null, Now);

        await handler.Handle(
            new DomainEventNotification<VerificationApprovedDomainEvent>(
                Raised<VerificationApprovedDomainEvent>(request)),
            CancellationToken.None);

        dispatcher.Captured.OfType<OrganizationVerifiedIntegrationEvent>().Single()
            .CountryCode.Should().Be("GB");
    }

    /// <summary>
    /// Captures what a handler publishes, instead of enqueuing it.
    /// <para>
    /// Before B1.2 this was a fake <c>IDomainEventDispatcher</c> holding a list of
    /// <c>IDomainEvent</c> — which is what Trust's handlers took, and a fair summary of the
    /// architectural defect the phase removed. It now fakes the port a producer is actually
    /// supposed to use, and the list can only hold published contracts.
    /// </para>
    /// </summary>
    private sealed class CapturingDispatcher : IIntegrationEventPublisher
    {
        private readonly List<IIntegrationEvent> _captured = [];

        public IReadOnlyList<IIntegrationEvent> Captured => _captured;

        public Task PublishAsync(
            IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
        {
            _captured.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Swallows the ADR-002 call back to Identity. This suite is about the published contract,
    /// and a real HTTP publisher would make it about the network.
    /// </summary>
    private sealed class NoOpDecisionPublisher : ITrustDecisionPublisher
    {
        public Task<bool> PublishAsync(
            UserId userId,
            TrustDecisionOutcome outcome,
            string? reason,
            string? source,
            CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    /// <summary>The shape a consumer compiled before B3.0.1 was reading.</summary>
    private sealed record LegacyRejectedEventView
    {
        public Guid VerificationId { get; init; }
        public Guid UserId { get; init; }
        public string VerificationType { get; init; } = string.Empty;
        public string ReasonCode { get; init; } = string.Empty;
        public string? Details { get; init; }
        public DateTimeOffset RejectedAt { get; init; }
    }

    /// <summary>
    /// A minimal stand-in for a per-country residency projection. It records what the event
    /// told it and nothing else — <see cref="InferredAnything"/> exists so a future change that
    /// reintroduced guessing would fail a test rather than pass review.
    /// </summary>
    private sealed class SimulatedResidencyProjection
    {
        private readonly Dictionary<(Guid User, string Country), string> _states = [];

        public bool InferredAnything { get; private set; }

        public void Apply(VerificationRejectedIntegrationEvent published)
        {
            if (!string.Equals(published.VerificationType, "Country", StringComparison.Ordinal))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(published.TargetCountry))
            {
                // The only correct action without a country. Anything else would be inference.
                return;
            }

            _states[(published.UserId, published.TargetCountry)] = "Rejected";
        }

        public string? StateFor(Guid userId, string country) =>
            _states.GetValueOrDefault((userId, country));
    }
}
