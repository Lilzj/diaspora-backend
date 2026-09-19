using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Whether anything a member does actually pays them.
/// <para>
/// <b>The scoring table had eighteen rules and three callers.</b> <c>ReputationScoring</c> says
/// what publishing a guide, verifying a phone or receiving a helpful vote is worth — points,
/// caps, cooldowns, eighteen of them. Exactly three were ever awarded, and two of those three
/// are penalties, so the only reliable way to move a member's reputation was to lose it.
/// Production agreed: one reputation profile, every counter at zero, and not one row in
/// <c>reputation_events</c> on an account whose phone was verified and whose diaspora
/// verification had been approved.
/// </para>
/// <para>
/// Nothing was broken in the usual sense. The rules were right, the service that applies them
/// was right, the profile holding the result was right. Nothing called them — the same shape as
/// the job adverts nobody could report and the country standings nobody could close.
/// </para>
/// <para>
/// These tests go through the product, not the consumers. A test that constructs an integration
/// event and hands it to a handler proves the handler works and says nothing about whether the
/// event is ever published, which is the half that was actually missing.
/// </para>
/// </summary>
public sealed class ReputationAwardTests : IdentityTestBase
{
    public ReputationAwardTests(CustomWebApplicationFactory factory) : base(factory) { }

    /// <summary>
    /// The first thing every member does, and it paid nothing. Identity publishes no
    /// phone-verified event — it publishes three events in total — so the signal is the access
    /// state leaving <c>PhoneVerificationPending</c>.
    /// </summary>
    [Fact]
    public async Task Verifying_a_phone_pays_the_member()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        await Factory.DrainOutboxAsync();

        var events = await EventsAsync(auth.User.Id);

        events.Should().Contain(entry => entry.ActionType == ReputationActions.PhoneVerified);

        events.Single(entry => entry.ActionType == ReputationActions.PhoneVerified)
            .Points.Should().BePositive();
    }

    /// <summary>
    /// The rule is capped at one occurrence. Delivery is at-least-once and the access state can
    /// cross that boundary more than once in a member's life, so the cap has to be the thing
    /// that holds — not the consumer assuming it fires once.
    /// </summary>
    [Fact]
    public async Task A_phone_is_only_paid_for_once()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        await Factory.DrainOutboxAsync();
        await Factory.DrainOutboxAsync();

        (await EventsAsync(auth.User.Id))
            .Count(entry => entry.ActionType == ReputationActions.PhoneVerified)
            .Should().Be(1);
    }

    /// <summary>
    /// An approved verification pays <b>once</b>.
    /// <para>
    /// <b>The assertion is the count, and it has to be.</b> Two consumers written for this file
    /// paid for approvals that <c>ReviewVerification</c> had been paying for all along, through
    /// the <c>ApprovalAward</c> every <c>IVerificationTypeHandler</c> carries — one listening to
    /// Trust's own approval event, one to the provider verification Providers raises when it
    /// applies the outcome of that same approval. A country approval produced two
    /// <c>CountryVerified</c> rows, thirty points each, against one verification id.
    /// </para>
    /// <para>
    /// Every test that asked whether the member had been paid passed throughout, because a
    /// member paid twice has still been paid. Only counting found it, which is why this test
    /// counts rather than asserting the row exists.
    /// </para>
    /// </summary>
    [Fact]
    public async Task An_approved_country_verification_pays_once_and_not_twice()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        var member = auth.Tokens.AccessToken;

        Api.Authenticate(member);

        var submitted = await HttpClient.PostAsJsonAsync(
            "/api/v1/trust/verifications/country",
            new
            {
                countryCode = "MU",
                documentType = "UtilityBill",
                documentReference = "media:" + Guid.NewGuid(),
                arrivalDate = new DateOnly(2019, 3, 1)
            });

        submitted.IsSuccessStatusCode.Should().BeTrue(
            await submitted.Content.ReadAsStringAsync());

        using var document = JsonDocument.Parse(await submitted.Content.ReadAsStringAsync());
        var requestId = document.RootElement.GetProperty("data")
            .GetProperty("verificationId").GetGuid();

        await SignInAsRoleAsync("Admin");

        (await HttpClient.PostAsJsonAsync(
                $"/api/v1/trust/verifications/{requestId}/assign", new { }))
            .EnsureSuccessStatusCode();

        (await HttpClient.PostAsJsonAsync(
                $"/api/v1/trust/verifications/{requestId}/review",
                new { decision = "Approve", notes = "Utility bill checks out." }))
            .EnsureSuccessStatusCode();

        await Factory.DrainOutboxAsync();

        (await EventsAsync(auth.User.Id))
            .Count(entry => entry.ActionType == ReputationActions.CountryVerified)
            .Should().Be(1);
    }

    /// <summary>
    /// A reputation profile has to exist before anything can be written to it, and a member who
    /// has never touched a trust feature has none. Every consumer provisions one first; if that
    /// were missing the award would be dropped rather than fail loudly.
    /// </summary>
    [Fact]
    public async Task The_points_reach_the_profile_and_not_only_the_ledger()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        await Factory.DrainOutboxAsync();

        using var scope = Factory.Services.CreateScope();

        var profile = await scope.ServiceProvider
            .GetRequiredService<IReputationProfileRepository>()
            .GetByUserAsync(UserId.From(auth.User.Id));

        profile.Should().NotBeNull();

        // The ledger and the profile are two writes. A row in reputation_events with the
        // counters still at zero is the failure this asserts against.
        profile!.CombinedScore.Should().BePositive();
        profile.PhoneVerified.Should().BeTrue();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private async Task<IReadOnlyList<ReputationEvent>> EventsAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();

        var page = await scope.ServiceProvider
            .GetRequiredService<IReputationProfileRepository>()
            .ListEventsAsync(UserId.From(userId), new PaginationRequest { PageSize = 50 });

        return page.Items;
    }
}
