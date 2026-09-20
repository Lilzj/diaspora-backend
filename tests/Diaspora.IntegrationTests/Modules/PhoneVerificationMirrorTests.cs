using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Contracts.Identity.V1.Events;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.Platform.Messaging.Outbox.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Diaspora.SharedKernel.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Identity verifies the phone; Trust has to hear about it.
/// <para>
/// <b>The checklist lied to every member on the platform.</b> <c>VerificationType.Phone</c> is
/// documented as "owned by Identity; mirrored here for the checklist", and nothing mirrored it.
/// Trust seeds <c>verifications.Phone</c> to <c>Pending</c> and changes it only when a Trust
/// verification request of that type is approved — and nothing anywhere created one. So the
/// trust centre showed "Phone Linked — Verify" to a member who had verified their phone during
/// registration, and kept showing it however many times they did it again.
/// </para>
/// <para>
/// Underneath the display, three capabilities declare <c>RequiresPhone</c> — <b>Commenting</b>,
/// <b>Community Updates</b> and the <b>Community Guide Programme</b> — and
/// <c>EligibilityEvaluator</c> reads a grant nothing could produce. Nothing enforces those
/// requirements today, so nobody was blocked; the day something does, commenting would have died
/// for every member at once.
/// </para>
/// </summary>
public sealed class PhoneVerificationMirrorTests : IdentityTestBase
{
    public PhoneVerificationMirrorTests(CustomWebApplicationFactory factory) : base(factory) { }

    /// <summary>
    /// The row in the screenshot. Registration verifies a phone; the checklist must say so.
    /// </summary>
    [Fact]
    public async Task A_verified_phone_reads_as_verified_on_the_trust_checklist()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        Api.Authenticate(auth.Tokens.AccessToken);

        await Factory.DrainOutboxAsync();

        (await PhoneStatusAsync()).Should().Be("Approved");
    }

    /// <summary>
    /// The half underneath the display. A grant is what <c>RequiresPhone</c> actually reads, and
    /// what the trust tier counts as "basics".
    /// </summary>
    [Fact]
    public async Task A_verified_phone_is_granted_in_trust()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        await Factory.DrainOutboxAsync();

        var profile = await TrustProfileAsync(auth.User.Id);

        profile.Should().NotBeNull();
        profile!.Grants.Should().Contain(VerificationType.Phone);
    }

    /// <summary>
    /// The payoff: a requirement nobody could ever meet now reads as met.
    /// <para>
    /// This asserts on the <b>checklist</b> rather than on the eligibility verdict, and the
    /// difference matters. The verdict for commenting is unmet for several reasons at once for a
    /// newly registered member, so "phone is not among the reasons" stayed true whether the
    /// grant existed or not — an assertion that passed while the bug was restored. The
    /// per-requirement row is the only thing that answers the question actually being asked.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_phone_requirement_reads_as_met()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        Api.Authenticate(auth.Tokens.AccessToken);

        await Factory.DrainOutboxAsync();

        var response = await HttpClient.GetAsync(
            "/api/v1/trust/feature-requirements?targetCountry=GB");

        response.IsSuccessStatusCode.Should().BeTrue(
            await response.Content.ReadAsStringAsync());

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        var phoneRows = document.RootElement.GetProperty("data")
            .EnumerateArray()
            .SelectMany(feature => feature.GetProperty("requirements").EnumerateArray())
            .Where(item => item.GetProperty("description").GetString() == "Verified Phone Number")
            .ToList();

        phoneRows.Should().NotBeEmpty(
            "three capabilities declare RequiresPhone, so the checklist must show the row");

        phoneRows.Should().OnlyContain(item => item.GetProperty("isMet").GetBoolean());
    }

    /// <summary>
    /// The automatic approval is what does the granting, and it used to do nothing but change a
    /// status: no grant, no award, nothing on the member's journey. Nobody noticed because the
    /// path was dead — phone and email are the only types that auto-approve, and neither was
    /// ever submitted.
    /// </summary>
    [Fact]
    public async Task The_automatic_approval_pays_the_award_the_handler_declares()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        await Factory.DrainOutboxAsync();

        var events = await EventsAsync(auth.User.Id);

        var paid = events.Where(entry => entry.ActionType == ReputationActions.PhoneVerified).ToList();

        paid.Should().ContainSingle();
        paid[0].Points.Should().BePositive();

        // The award comes from PhoneVerificationHandler.ApprovalAward, applied against the
        // verification. A row with no source is the old consumer, which must be gone.
        paid[0].SourceType.Should().Be("verification");
    }

    /// <summary>
    /// Delivery is at-least-once, and this type auto-approves — so by the time a duplicate
    /// arrives there is no open request left to find. Deduplication has to be the correlation
    /// identifier, not an open-case check.
    /// </summary>
    [Fact]
    public async Task A_redelivered_phone_verification_opens_no_second_case()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        await Factory.DrainOutboxAsync();
        await Factory.DrainOutboxAsync();
        await Factory.DrainOutboxAsync();

        (await EventsAsync(auth.User.Id))
            .Count(entry => entry.ActionType == ReputationActions.PhoneVerified)
            .Should().Be(1);

        var profile = await TrustProfileAsync(auth.User.Id);

        profile!.Badges
            .Count(badge => badge.Badge == BadgeType.PhoneVerified && badge.IsActive)
            .Should().Be(1);
    }

    /// <summary>
    /// The repair for accounts verified before anything published the fact.
    /// <para>
    /// An operator running it cannot know which accounts are stale, so running it against one
    /// that is already correct has to be free. It is the same correlation identifier, so the
    /// coordinator finds the existing case and opens nothing.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Re_announcing_a_phone_that_is_already_mirrored_changes_nothing()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        await Factory.DrainOutboxAsync();

        await SignInAsRoleAsync("Admin");

        var repaired = await HttpClient.PostAsync(
            $"/api/v1/admin/accounts/{auth.User.Id}/republish-phone-verification", content: null);

        repaired.IsSuccessStatusCode.Should().BeTrue(
            await repaired.Content.ReadAsStringAsync());

        await Factory.DrainOutboxAsync();

        (await EventsAsync(auth.User.Id))
            .Count(entry => entry.ActionType == ReputationActions.PhoneVerified)
            .Should().Be(1);
    }

    /// <summary>
    /// A repair tool must not be able to grant what was never proved. Announcing a verification
    /// that did not happen would hand the member a phone they do not hold.
    /// </summary>
    [Fact]
    public async Task Re_announcing_refuses_a_member_whose_phone_is_not_verified()
    {
        var phone = NextPhoneNumber();
        await RegisterAsync(phone);

        var unverified = await FindMemberIdAsync(phone);

        await SignInAsRoleAsync("Admin");

        var response = await HttpClient.PostAsync(
            $"/api/v1/admin/accounts/{unverified}/republish-phone-verification", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// The repair must actually publish something.
    /// <para>
    /// <b>It did not, and it said it had.</b> Publishing enqueues into the outbox inside the
    /// handler's own transaction, and the handler committed nothing — so the route answered 200,
    /// logged "Re-announced the verified phone", and left the outbox empty. It was run against a
    /// real account in production before anything noticed.
    /// </para>
    /// <para>
    /// The two tests either side of this one both passed throughout, because both assert on a
    /// member whose mirror is <em>already</em> correct: "nothing changed" is exactly what a
    /// handler that does nothing produces. Only counting what was published catches it.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Re_announcing_publishes_the_fact_rather_than_only_saying_so()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        await SignInAsRoleAsync("Admin");

        // Counted rather than drained to zero: signing in as staff registers two more members
        // of its own, and each of those publishes the same event. The delta is the only number
        // that belongs to this route.
        var before = await PendingPhoneEventsAsync();

        var repaired = await HttpClient.PostAsync(
            $"/api/v1/admin/accounts/{auth.User.Id}/republish-phone-verification", content: null);

        repaired.IsSuccessStatusCode.Should().BeTrue(
            await repaired.Content.ReadAsStringAsync());

        (await PendingPhoneEventsAsync()).Should().Be(
            before + 1, "a 200 from this route has to mean a message was enqueued");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Undelivered phone-verified events, which is what "published" means here.</summary>
    private async Task<int> PendingPhoneEventsAsync()
    {
        using var scope = Factory.Services.CreateScope();

        var pending = await scope.ServiceProvider
            .GetRequiredService<IOutboxQueries>()
            .ListPendingAsync(new OutboxQuery(
                EventType: typeof(UserPhoneVerifiedIntegrationEvent).FullName, Limit: 50));

        return pending.Count;
    }

    private async Task<string?> PhoneStatusAsync()
    {
        var response = await HttpClient.GetAsync("/api/v1/trust/profile");

        response.IsSuccessStatusCode.Should().BeTrue(
            await response.Content.ReadAsStringAsync());

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return document.RootElement.GetProperty("data")
            .GetProperty("verifications")
            .GetProperty("Phone")
            .GetString();
    }

    /// <summary>The account behind a phone number, straight from the store.</summary>
    private async Task<Guid> FindMemberIdAsync(string phoneNumber)
    {
        using var scope = Factory.Services.CreateScope();

        var user = await scope.ServiceProvider
            .GetRequiredService<IUserRepository>()
            .GetByPhoneNumberAsync(PhoneNumber.Create(phoneNumber));

        user.Should().NotBeNull();

        return user!.Id.Value;
    }

    private async Task<TrustProfile?> TrustProfileAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();

        return await scope.ServiceProvider
            .GetRequiredService<ITrustProfileRepository>()
            .GetByUserAsync(UserId.From(userId));
    }

    private async Task<IReadOnlyList<ReputationEvent>> EventsAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();

        var page = await scope.ServiceProvider
            .GetRequiredService<IReputationProfileRepository>()
            .ListEventsAsync(UserId.From(userId), new PaginationRequest { PageSize = 50 });

        return page.Items;
    }
}
