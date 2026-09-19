using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Submitting a verification through the app, and what it does to the member's access state.
/// <para>
/// <b>Nothing moved anybody, by three faults stacked on each other.</b> The deployment had no
/// <c>Trust:Decisions</c> section, so every decision was logged as undeliverable and dropped —
/// which hid the second fault, that submission published <c>UnderReview</c>, targeting
/// <c>VerificationUnderReview</c>, a rung the transition table does not allow from
/// <c>LimitedAccess</c>. And the third: nothing published anything when a reviewer picked a
/// request up, so even a member who had somehow reached <c>VerificationPending</c> could not be
/// approved, because <c>Granted</c> targets <c>Verified</c> and only
/// <c>VerificationUnderReview</c> reaches it.
/// </para>
/// <para>
/// The ladder is <c>LimitedAccess -> VerificationPending -> VerificationUnderReview -> Verified</c>
/// and each test below is one rung. They are worth having separately: a single end-to-end test
/// would have gone green the moment any one of the three was fixed.
/// </para>
/// <para>
/// This exercises <b>Trust's</b> submission route, the one the app calls. Identity's own
/// <c>/access/verification-requests</c> moves the state directly and always worked, which is why
/// every other suite's verified members were never affected by any of this.
/// </para>
/// </summary>
public sealed class VerificationLadderTests : IdentityTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public VerificationLadderTests(CustomWebApplicationFactory factory) : base(factory) { }

    /// <summary>
    /// The first rung. Submitting through Trust moves the member off LimitedAccess, so the app
    /// stops offering "Start Verification" to somebody who has already started.
    /// </summary>
    [Fact]
    public async Task Submitting_moves_the_member_to_pending()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        (await ReadAccessStateAsync()).Should().Be("LimitedAccess");

        var submitted = await SubmitDiasporaVerificationAsync();

        submitted.StatusCode.Should().Be(
            HttpStatusCode.Created, await submitted.Content.ReadAsStringAsync());

        (await ReadAccessStateAsync()).Should().Be("VerificationPending");
    }

    /// <summary>
    /// The second rung, and the one nothing published at all. A reviewer taking the request must
    /// move the member to UnderReview, or the approval that follows has nowhere legal to go.
    /// </summary>
    [Fact]
    public async Task Taking_the_request_moves_the_member_to_under_review()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var requestId = await ReadIdAsync(await SubmitDiasporaVerificationAsync());
        var member = auth.Tokens.AccessToken;

        await SignInAsRoleAsync("Admin");

        var assigned = await HttpClient.PostAsJsonAsync(
            $"/api/v1/trust/verifications/{requestId}/assign", new { });

        assigned.IsSuccessStatusCode.Should().BeTrue(
            await assigned.Content.ReadAsStringAsync());

        Api.Authenticate(member);

        (await ReadAccessStateAsync()).Should().Be("VerificationUnderReview");
    }

    /// <summary>
    /// The whole ladder. A member who submits and is approved ends up Verified — which is what
    /// unlocks every Verified-tier capability on the platform, and what no member could reach.
    /// </summary>
    [Fact]
    public async Task An_approved_verification_leaves_the_member_verified()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var requestId = await ReadIdAsync(await SubmitDiasporaVerificationAsync());
        var member = auth.Tokens.AccessToken;

        await SignInAsRoleAsync("Admin");

        (await HttpClient.PostAsJsonAsync(
                $"/api/v1/trust/verifications/{requestId}/assign", new { }))
            .EnsureSuccessStatusCode();

        var approved = await HttpClient.PostAsJsonAsync(
            $"/api/v1/trust/verifications/{requestId}/review",
            new { decision = "Approve", notes = "Documents check out." });

        approved.IsSuccessStatusCode.Should().BeTrue(
            await approved.Content.ReadAsStringAsync());

        Api.Authenticate(member);

        (await ReadAccessStateAsync()).Should().Be("Verified");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The shape the app sends. Nationality and residence must differ — the whole point of a
    /// diaspora claim is living somewhere other than where you are from.
    /// </summary>
    private async Task<HttpResponseMessage> SubmitDiasporaVerificationAsync() =>
        await HttpClient.PostAsJsonAsync(
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

    private async Task<string> ReadAccessStateAsync()
    {
        var response = await Api.GetAccessStateAsync();

        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return document.RootElement.GetProperty("data").GetProperty("state").GetString()!;
    }

    private static async Task<Guid> ReadIdAsync(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.Should().BeTrue(
            await response.Content.ReadAsStringAsync());

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        var data = document.RootElement.GetProperty("data");

        foreach (var name in new[] { "verificationId", "requestId", "id" })
        {
            if (data.TryGetProperty(name, out var value))
            {
                return value.GetGuid();
            }
        }

        throw new InvalidOperationException(
            "The submission response carried no identifier: " + data);
    }
}
