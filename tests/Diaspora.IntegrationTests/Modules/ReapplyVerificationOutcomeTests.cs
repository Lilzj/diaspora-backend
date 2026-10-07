using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Contracts.Identity.V1.Responses;
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
/// Finishing an approval that never granted anything.
/// <para>
/// <b>An approval is two writes and only one of them is the status.</b> Marking a request
/// <c>Approved</c> is what a reviewer sees; recording the grant, paying the handler's award and
/// reconciling badges is what the approval is <em>for</em>. Anything that sets the status by
/// another route does the first and not the second.
/// </para>
/// <para>
/// The platform's first Admin was bootstrapped by hand in SQL, because there was no staff
/// account to approve anything and no route to make one. Their diaspora verification read
/// <c>Approved</c> with an empty grant set, which left them on the <c>Basic</c> tier — and
/// <c>ViewerContextResolver</c> reads that tier, so Community treated the only Admin on the
/// platform as an unverified member.
/// </para>
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class ReapplyVerificationOutcomeTests : PooledIdentityTestBase
{
    public ReapplyVerificationOutcomeTests(CustomWebApplicationFactory factory) : base(factory) { }

    /// <summary>
    /// The repair. A grant that never landed, landing.
    /// </summary>
    [Fact]
    public async Task Re_applying_grants_what_the_approval_should_have_granted()
    {
        var (member, verificationId) = await ApprovedButUngrantedAsync();

        var applied = await HttpClient.PostAsync(
            $"/api/v1/trust/verifications/{verificationId}/reapply-outcome", content: null);

        applied.IsSuccessStatusCode.Should().BeTrue(
            await applied.Content.ReadAsStringAsync());

        var profile = await TrustProfileAsync(member);

        profile!.Grants.Should().Contain(VerificationType.Diaspora);
        profile.Tier.Should().Be(TrustTier.Verified, "a diaspora grant is what lifts the tier");
    }

    /// <summary>
    /// <b>Twice must not pay twice.</b> The award behind a country verification declares no
    /// <c>MaxOccurrences</c>, so nothing underneath stops a second run — this module has already
    /// produced that defect three times, once paying thirty points twice for one approval.
    /// </summary>
    [Fact]
    public async Task Re_applying_a_second_time_is_not_paid_again()
    {
        var (member, verificationId) = await ApprovedButUngrantedAsync();

        (await HttpClient.PostAsync(
                $"/api/v1/trust/verifications/{verificationId}/reapply-outcome", content: null))
            .EnsureSuccessStatusCode();

        var paidOnce = await CountryPointsAsync(member);

        var again = await HttpClient.PostAsync(
            $"/api/v1/trust/verifications/{verificationId}/reapply-outcome", content: null);

        // Accepted since 7 Oct 2026: the standing half is skipped and only the grant to the
        // account is re-sent, which is how a member stranded on VerificationPending is repaired.
        // What must not happen is a second payment.
        again.StatusCode.Should().Be(HttpStatusCode.OK);

        (await CountryPointsAsync(member)).Should().Be(paidOnce);
    }

    /// <summary>
    /// It finishes decisions; it does not make them. A pending request has no outcome to apply,
    /// and admitting one here would be a second approval route with none of the guards.
    /// </summary>
    [Fact]
    public async Task A_verification_nobody_approved_is_refused()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();

        Api.Authenticate(auth.Tokens.AccessToken);

        var verificationId = await ReadIdAsync(await SubmitDiasporaAsync());

        await SignInAsRoleAsync("Admin");

        var applied = await HttpClient.PostAsync(
            $"/api/v1/trust/verifications/{verificationId}/reapply-outcome", content: null);

        applied.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>
    /// The country-scoped branch. A residency claim is judged by the standing for <em>that</em>
    /// country rather than by the grant set, because the grant set is keyed by type: a member
    /// verified in one country already holds <c>Country</c>, and a type-only check would refuse
    /// to finish the approval for their second one — which the relocation flow makes ordinary.
    /// </summary>
    [Fact]
    public async Task A_country_the_member_already_stands_in_is_not_applied_again()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        var member = auth.User.Id;

        Api.Authenticate(auth.Tokens.AccessToken);

        var verificationId = await ReadIdAsync(await SubmitCountryAsync());

        await SignInAsRoleAsync("Admin");

        (await HttpClient.PostAsJsonAsync(
                $"/api/v1/trust/verifications/{verificationId}/assign", new { }))
            .EnsureSuccessStatusCode();

        (await HttpClient.PostAsJsonAsync(
                $"/api/v1/trust/verifications/{verificationId}/review",
                new { decision = "Approve", notes = "Checks out." }))
            .EnsureSuccessStatusCode();

        // Grants emptied, standing left alone: the type is gone but the country is still proven.
        await StripTheGrantAsync(auth.User.Id);

        var paidOnce = await CountryPointsAsync(member);

        var applied = await HttpClient.PostAsync(
            $"/api/v1/trust/verifications/{verificationId}/reapply-outcome", content: null);

        // The grant to the account is re-sent (200); the standing is not touched again.
        applied.StatusCode.Should().Be(HttpStatusCode.OK);
        (await CountryPointsAsync(member)).Should().Be(paidOnce);
    }

    /// <summary>
    /// <b>The defect of 7 Oct 2026.</b> The console approves straight from the queue, so no
    /// assignment ever moved the account onto the review rung, and Identity refused the grant:
    /// the request said Approved, the tier said Verified, and the account stayed on
    /// VerificationPending — so the app kept offering "Start verification".
    /// </summary>
    [Fact]
    public async Task Approving_without_assigning_first_verifies_the_account()
    {
        // Phone-verified only, like a real new member. RegisterVerifiedMemberAsync would not
        // do: it sends UnderReview and Granted to Identity by hand, which is the very step the
        // product never takes — this test passed without the fix until it stopped using it.
        var (_, auth) = await RegisterAndVerifyAsync();

        Api.Authenticate(auth.Tokens.AccessToken);

        var verificationId = await ReadIdAsync(await SubmitDiasporaAsync());

        (await IdentityApiClient.ReadDataAsync<AccessStateResponse>(await Api.GetAccessStateAsync()))
            .State.Should().Be("VerificationPending", "the premise: submitting is what puts the account on the ladder");

        await SignInAsRoleAsync("Admin");

        (await HttpClient.PostAsJsonAsync(
                $"/api/v1/trust/verifications/{verificationId}/review",
                new { decision = "Approve" }))
            .EnsureSuccessStatusCode();

        // The reviewer sees who the person is, not only an identifier.
        using (var detail = JsonDocument.Parse(await HttpClient.GetStringAsync(
                   $"/api/v1/trust/verifications/{verificationId}")))
        {
            detail.RootElement.GetProperty("data").GetProperty("memberName").GetString()
                .Should().NotBeNullOrWhiteSpace();
        }

        Api.Authenticate(auth.Tokens.AccessToken);

        var state = await IdentityApiClient.ReadDataAsync<AccessStateResponse>(
            await Api.GetAccessStateAsync());

        state.State.Should().Be("Verified");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A member holding an approved verification whose outcome never reached their standing.
    /// <para>
    /// Reproduced the way it happened: the request is approved through the real review route,
    /// and the grant is then removed — which is what a status set outside
    /// <c>ReviewVerification</c> leaves behind. Leaves an Admin authenticated.
    /// </para>
    /// </summary>
    private async Task<(Guid Member, Guid VerificationId)> ApprovedButUngrantedAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();

        Api.Authenticate(auth.Tokens.AccessToken);

        var verificationId = await ReadIdAsync(await SubmitDiasporaAsync());

        await SignInAsRoleAsync("Admin");

        (await HttpClient.PostAsJsonAsync(
                $"/api/v1/trust/verifications/{verificationId}/assign", new { }))
            .EnsureSuccessStatusCode();

        (await HttpClient.PostAsJsonAsync(
                $"/api/v1/trust/verifications/{verificationId}/review",
                new { decision = "Approve", notes = "Checks out." }))
            .EnsureSuccessStatusCode();

        await StripTheGrantAsync(auth.User.Id);

        return (auth.User.Id, verificationId);
    }

    /// <summary>
    /// Puts the profile back to what a hand-applied approval leaves: the request says Approved
    /// and the standing knows nothing about it.
    /// </summary>
    private async Task StripTheGrantAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<Persistence.DiasporaDbContext>();

        var profile = await new TrustProfileRepository(context)
            .GetByUserAsync(UserId.From(userId));

        profile.Should().NotBeNull();

        // The backing field, which is how the configuration maps it: a shadow "grants"
        // property does not exist.
        context.Entry(profile!)
            .Property<HashSet<VerificationType>>("_grants")
            .CurrentValue = [];

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// The shape the app sends. Nationality and residence must differ — the whole point of a
    /// diaspora claim is living somewhere other than where you are from.
    /// </summary>
    private Task<HttpResponseMessage> SubmitDiasporaAsync() =>
        HttpClient.PostAsJsonAsync(
            "/api/v1/trust/verifications/diaspora",
            new
            {
                nationality = "NG",
                residenceCountry = "MU",
                documentType = "Passport",
                issueCountry = "NG",
                documentReference = "media:" + Guid.NewGuid(),
                selfieReference = "media:" + Guid.NewGuid()
            });

    private Task<HttpResponseMessage> SubmitCountryAsync() =>
        HttpClient.PostAsJsonAsync(
            "/api/v1/trust/verifications/country",
            new
            {
                countryCode = "MU",
                documentType = "UtilityBill",
                documentReference = "media:" + Guid.NewGuid(),
                arrivalDate = new DateOnly(2019, 3, 1)
            });

    private async Task<int> CountryPointsAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();

        var page = await scope.ServiceProvider
            .GetRequiredService<IReputationProfileRepository>()
            .ListEventsAsync(UserId.From(userId), new PaginationRequest { PageSize = 50 });

        return page.Items
            .Where(entry => entry.ActionType == ReputationActions.CountryVerified)
            .Sum(entry => entry.Points);
    }

    private async Task<TrustProfile?> TrustProfileAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();

        return await scope.ServiceProvider
            .GetRequiredService<ITrustProfileRepository>()
            .GetByUserAsync(UserId.From(userId));
    }

    private static async Task<Guid> ReadIdAsync(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.Should().BeTrue(
            await response.Content.ReadAsStringAsync());

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return document.RootElement.GetProperty("data").GetProperty("verificationId").GetGuid();
    }
}
