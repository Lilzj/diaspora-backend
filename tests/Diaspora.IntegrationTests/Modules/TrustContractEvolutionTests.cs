using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.Residency;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Platform.Messaging;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// B3.0.1 end to end: Trust publishes, Location projects, and no one guesses.
/// <para>
/// The events are dispatched through the host's real <see cref="IDomainEventDispatcher"/> —
/// the same call Trust's translation handlers make — so this exercises the real MediatR
/// wiring, the real Location handlers, the real repositories and the real database. What it
/// deliberately does not do is drive a staff review over HTTP: minting a moderator session
/// would be testing Identity's authorisation, not this contract.
/// </para>
/// <para>
/// The consumer under test is B4's actual projection, not a stand-in. The stand-in version
/// lives in the unit suite and proves the contract is sufficient for <em>any</em> per-country
/// consumer; this one proves the platform's real consumer now uses it.
/// </para>
/// </summary>
[Collection(FinanceCollection.Name)]
public sealed class TrustContractEvolutionTests : PooledIdentityTestBase
{
    public TrustContractEvolutionTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── The whole lifecycle, each event carrying its own country ─────────

    /// <summary>
    /// Approve → reject → expire → revoke, each attributed to the country it names, with no
    /// prior state required for any of them.
    /// </summary>
    [Fact]
    public async Task Every_lifecycle_event_projects_onto_the_country_it_names()
    {
        var member = Guid.NewGuid();

        await DispatchAsync(new VerificationApprovedIntegrationEvent
        {
            VerificationId = Guid.NewGuid(),
            UserId = member,
            VerificationType = "Country",
            TargetCountry = "GB",
            Partial = false,
            ApprovedAt = DateTimeOffset.UtcNow.AddDays(-4)
        });

        (await StateOfAsync(member, "GB")).Should().Be(ResidencyVerificationState.Verified);

        await DispatchAsync(new VerificationRejectedIntegrationEvent
        {
            VerificationId = Guid.NewGuid(),
            UserId = member,
            VerificationType = "Country",
            TargetCountry = "GB",
            ReasonCode = "ILLEGIBLE_DOCUMENT",
            RejectedAt = DateTimeOffset.UtcNow.AddDays(-3)
        });

        (await StateOfAsync(member, "GB")).Should().Be(ResidencyVerificationState.Rejected);

        await DispatchAsync(new VerificationExpiredIntegrationEvent
        {
            VerificationId = Guid.NewGuid(),
            UserId = member,
            VerificationType = "Country",
            TargetCountry = "GB",
            ExpiredAt = DateTimeOffset.UtcNow.AddDays(-2)
        });

        (await StateOfAsync(member, "GB")).Should().Be(ResidencyVerificationState.Expired);

        await DispatchAsync(new VerificationRevokedIntegrationEvent
        {
            VerificationId = Guid.NewGuid(),
            UserId = member,
            VerificationType = "Country",
            TargetCountry = "GB",
            Reason = "Evidence found to be forged",
            RevokedAt = DateTimeOffset.UtcNow.AddDays(-1)
        });

        (await StateOfAsync(member, "GB")).Should().Be(ResidencyVerificationState.Revoked);
    }

    // ── The gap the patch closes ─────────────────────────────────────────

    /// <summary>
    /// **The reason B3.0.1 exists.** Before it, this refusal reached Location with nothing to
    /// attribute it to, and the handler correctly recorded nothing. Now it lands on the right
    /// country from a cold projection — no approval seen first, no identifier to match, no
    /// inference.
    /// </summary>
    [Fact]
    public async Task A_refusal_projects_onto_its_country_from_a_cold_projection()
    {
        var member = Guid.NewGuid();

        await DispatchAsync(new VerificationRejectedIntegrationEvent
        {
            VerificationId = Guid.NewGuid(),
            UserId = member,
            VerificationType = "Country",
            TargetCountry = "MU",
            ReasonCode = "RESIDENCE_NOT_ESTABLISHED",
            Details = "Utility bill predates the claimed arrival.",
            RejectedAt = DateTimeOffset.UtcNow
        });

        var residency = await FindAsync(member, "MU");

        residency.Should().NotBeNull("the refusal names its country outright");
        residency!.State.Should().Be(ResidencyVerificationState.Rejected);
        residency.Reason.Should().Be("RESIDENCE_NOT_ESTABLISHED");
    }

    /// <summary>
    /// Two claims in flight, refused out of submission order. A most-recent-submission
    /// heuristic — the only inference available before B3.0.1 — puts both on the wrong country.
    /// </summary>
    [Fact]
    public async Task Two_claims_in_flight_are_refused_onto_the_right_countries()
    {
        var member = Guid.NewGuid();

        await DispatchAsync(new VerificationRejectedIntegrationEvent
        {
            VerificationId = Guid.NewGuid(),
            UserId = member,
            VerificationType = "Country",
            TargetCountry = "MU",
            ReasonCode = "INVALID_DOCUMENT",
            RejectedAt = DateTimeOffset.UtcNow.AddMinutes(-2)
        });

        await DispatchAsync(new VerificationRejectedIntegrationEvent
        {
            VerificationId = Guid.NewGuid(),
            UserId = member,
            VerificationType = "Country",
            TargetCountry = "GB",
            ReasonCode = "INVALID_DOCUMENT",
            RejectedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        });

        (await StateOfAsync(member, "MU")).Should().Be(ResidencyVerificationState.Rejected);
        (await StateOfAsync(member, "GB")).Should().Be(ResidencyVerificationState.Rejected);
    }

    /// <summary>
    /// One member, two countries, one refused. The other must be untouched — which is the
    /// failure a guess would have produced.
    /// </summary>
    [Fact]
    public async Task Refusing_one_country_leaves_the_members_other_country_alone()
    {
        var member = Guid.NewGuid();

        await DispatchAsync(new VerificationApprovedIntegrationEvent
        {
            VerificationId = Guid.NewGuid(),
            UserId = member,
            VerificationType = "Country",
            TargetCountry = "GB",
            Partial = false,
            ApprovedAt = DateTimeOffset.UtcNow.AddDays(-1)
        });

        await DispatchAsync(new VerificationRejectedIntegrationEvent
        {
            VerificationId = Guid.NewGuid(),
            UserId = member,
            VerificationType = "Country",
            TargetCountry = "MU",
            ReasonCode = "INVALID_DOCUMENT",
            RejectedAt = DateTimeOffset.UtcNow
        });

        (await StateOfAsync(member, "GB")).Should().Be(ResidencyVerificationState.Verified);
        (await StateOfAsync(member, "MU")).Should().Be(ResidencyVerificationState.Rejected);
    }

    // ── Backwards compatibility, in the running host ─────────────────────

    /// <summary>
    /// A payload published before B3.0.1 names no country. The refusal handler still records
    /// nothing rather than guessing — the same conservative behaviour B4 shipped with, which
    /// is what makes a rolling deploy safe in both directions.
    /// </summary>
    [Fact]
    public async Task A_pre_patch_refusal_still_records_nothing()
    {
        var member = Guid.NewGuid();

        await DispatchAsync(new VerificationRejectedIntegrationEvent
        {
            VerificationId = Guid.NewGuid(),
            UserId = member,
            VerificationType = "Country",
            TargetCountry = null,
            ReasonCode = "INVALID_DOCUMENT",
            RejectedAt = DateTimeOffset.UtcNow
        });

        (await FindAsync(member, "GB")).Should().BeNull();
        (await FindAsync(member, "MU")).Should().BeNull();
    }

    /// <summary>
    /// A pre-patch expiry names no country either — but the consumer can still attribute it by
    /// the verification id the approval recorded. The fallback is kept precisely so an event
    /// already in flight when the deploy happens is not lost.
    /// </summary>
    [Fact]
    public async Task A_pre_patch_expiry_is_still_attributed_by_verification_id()
    {
        var member = Guid.NewGuid();
        var verificationId = Guid.NewGuid();

        await DispatchAsync(new VerificationApprovedIntegrationEvent
        {
            VerificationId = verificationId,
            UserId = member,
            VerificationType = "Country",
            TargetCountry = "GB",
            Partial = false,
            ApprovedAt = DateTimeOffset.UtcNow.AddDays(-1)
        });

        await DispatchAsync(new VerificationExpiredIntegrationEvent
        {
            VerificationId = verificationId,
            UserId = member,
            VerificationType = "Country",
            TargetCountry = null,
            ExpiredAt = DateTimeOffset.UtcNow
        });

        (await StateOfAsync(member, "GB")).Should().Be(ResidencyVerificationState.Expired);
    }

    /// <summary>The same fallback for a revocation.</summary>
    [Fact]
    public async Task A_pre_patch_revocation_is_still_attributed_by_verification_id()
    {
        var member = Guid.NewGuid();
        var verificationId = Guid.NewGuid();

        await DispatchAsync(new VerificationApprovedIntegrationEvent
        {
            VerificationId = verificationId,
            UserId = member,
            VerificationType = "Country",
            TargetCountry = "MU",
            Partial = false,
            ApprovedAt = DateTimeOffset.UtcNow.AddDays(-1)
        });

        await DispatchAsync(new VerificationRevokedIntegrationEvent
        {
            VerificationId = verificationId,
            UserId = member,
            VerificationType = "Country",
            TargetCountry = null,
            Reason = "Evidence forged",
            RevokedAt = DateTimeOffset.UtcNow
        });

        (await StateOfAsync(member, "MU")).Should().Be(ResidencyVerificationState.Revoked);
    }

    // ── Scope of the projection ──────────────────────────────────────────

    /// <summary>
    /// A diaspora refusal names no community, and must not create one. This is the case a
    /// naive "project whatever country is on the event" implementation would get wrong once
    /// Trust started putting a residence country on non-country claims.
    /// </summary>
    [Fact]
    public async Task A_non_country_refusal_creates_no_residency()
    {
        var member = Guid.NewGuid();

        await DispatchAsync(new VerificationRejectedIntegrationEvent
        {
            VerificationId = Guid.NewGuid(),
            UserId = member,
            VerificationType = "Diaspora",
            TargetCountry = null,
            ReasonCode = "NOT_DIASPORA_ELIGIBLE",
            RejectedAt = DateTimeOffset.UtcNow
        });

        (await FindAsync(member, "GB")).Should().BeNull();
    }

    /// <summary>
    /// Delivery is at-least-once and unordered. A refusal that arrives after a later approval
    /// must not overwrite it — the ordering guard predates this patch and still holds with the
    /// new attribution path.
    /// </summary>
    [Fact]
    public async Task An_out_of_order_refusal_does_not_overwrite_a_later_approval()
    {
        var member = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await DispatchAsync(new VerificationApprovedIntegrationEvent
        {
            VerificationId = Guid.NewGuid(),
            UserId = member,
            VerificationType = "Country",
            TargetCountry = "GB",
            Partial = false,
            ApprovedAt = now
        });

        await DispatchAsync(new VerificationRejectedIntegrationEvent
        {
            VerificationId = Guid.NewGuid(),
            UserId = member,
            VerificationType = "Country",
            TargetCountry = "GB",
            ReasonCode = "INVALID_DOCUMENT",
            RejectedAt = now.AddDays(-1)
        });

        (await StateOfAsync(member, "GB")).Should().Be(
            ResidencyVerificationState.Verified,
            "an older decision must never displace a newer one");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Publishes exactly as Trust's translation handlers do, then drains.
    /// <para>
    /// B1.1 changed what the first line means. Publishing no longer runs Location's handlers
    /// inline; it writes a durable outbox row that commits with the producer's transaction, and
    /// delivery happens on a later pass. The save and the drain below are that pass, made
    /// explicit — which is more honest than the old arrangement, where the assertion that
    /// followed was in truth asserting on a synchronous side effect that the platform had never
    /// promised.
    /// </para>
    /// <para>
    /// <b>B1.2 changed which port it goes through, and this helper is a small worked example of
    /// the migration.</b> It used to resolve <c>IDomainEventDispatcher</c> and hand it a
    /// collection containing an integration event — the same shortcut Trust and Community took in
    /// production, and legal for the same reason. That line is now a compile error (CS0029:
    /// cannot convert <c>IntegrationEvent</c> to <c>IDomainEvent</c>), which is precisely the
    /// outcome the phase was for. The fix is the same one every call site took: resolve
    /// <see cref="IIntegrationEventPublisher"/> instead.
    /// </para>
    /// <para>
    /// What is under test is unchanged: Trust's real payload, reaching B4's real consumer,
    /// through the host's real wiring.
    /// </para>
    /// </summary>
    private async Task DispatchAsync(Contracts.Events.IntegrationEvent published)
    {
        using (var scope = Factory.Services.CreateScope())
        {
            var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();

            await publisher.PublishAsync(published);

            // A producer's own SaveChanges commits the aggregate and the message together.
            // There is no aggregate here, so this stands in for that commit.
            await scope.ServiceProvider
                .GetRequiredService<Domain.Abstractions.IUnitOfWork>()
                .SaveChangesAsync();
        }

        await Factory.DrainOutboxAsync();
    }

    /// <summary>Reads the projection from a fresh scope, so nothing comes from a tracker.</summary>
    private async Task<VerifiedResidency?> FindAsync(Guid userId, string countryCode)
    {
        using var scope = Factory.Services.CreateScope();

        var residencies = scope.ServiceProvider.GetRequiredService<IVerifiedResidencyRepository>();

        return await residencies.GetAsync(
            UserId.From(userId), CountryCode.Create(countryCode));
    }

    private async Task<ResidencyVerificationState?> StateOfAsync(Guid userId, string countryCode) =>
        (await FindAsync(userId, countryCode))?.State;
}
