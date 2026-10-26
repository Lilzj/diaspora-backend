using System.Net;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Trust.Domain;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// <c>GET /trust/reputation/rules</c>, through the real route.
/// <para>
/// The reputation screen's "How to earn XP" was a sentence typed into the app: guides +50 (right),
/// confirming alerts +15 (confirming pays the <em>author</em>, five), residency documents +100
/// (thirty), helpful votes +10 (two). The screen builds it from this list now.
/// </para>
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class ReputationRulesTests : PooledIdentityTestBase
{
    public ReputationRulesTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task The_rules_list_only_what_is_actually_paid()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync("/api/v1/trust/reputation/rules");
        var rules = await IdentityApiClient.ReadDataAsync<List<RuleView>>(response);

        rules.Select(rule => rule.ActionType).Should().NotContain(
            ReputationActions.ProfileCompleted, "nothing pays it, and the list is read as promises");
        rules.Select(rule => rule.ActionType).Should().Contain(
            ReputationActions.ReportResolved, "an upheld report pays its reporter since step 3e");

        // Paid since REP-13: the first profile photo, and a post the community verifies.
        rules.Should().ContainEquivalentOf(new RuleView(
            ReputationActions.UpdateVerified, "Updates confirmed by the community", "contribution", 15, null, null, true, 200));
        rules.Should().ContainEquivalentOf(new RuleView(
            ReputationActions.ProfilePhotoAdded, "Profile photo added", "verification", 5, 1, null, false));

        rules.Should().ContainEquivalentOf(new RuleView(
            ReputationActions.GuidePublished, "Guides published", "contribution", 50, null, 1, true));
        rules.Should().ContainEquivalentOf(new RuleView(
            ReputationActions.CountryVerified, "Countries verified", "verification", 30, 1, null, true));
        rules.Should().ContainEquivalentOf(new RuleView(
            ReputationActions.ViolationRecorded, "Policy violations", "trust", -75, null, null, false));
    }

    /// <summary>
    /// D-2: points only for confirmed outcomes. The rules say which pay points as well as XP, so the
    /// app can show what earns points from the platform's own list (CS-C4).
    /// </summary>
    [Fact]
    public async Task The_rules_say_which_confirmed_outcomes_also_pay_points()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var rules = await IdentityApiClient.ReadDataAsync<List<RuleView>>(
            await HttpClient.GetAsync("/api/v1/trust/reputation/rules"));

        rules.Where(rule => rule.CreditPoints is not null)
            .ToDictionary(rule => rule.ActionType, rule => rule.CreditPoints)
            .Should().BeEquivalentTo(new Dictionary<string, int?>
            {
                [ReputationActions.GuideApproved] = 250,
                [ReputationActions.UpdateVerified] = 200,
                [ReputationActions.ReferralQualified] = 250
            });
    }

    [Fact]
    public async Task The_rules_may_be_cached_by_the_caller_alone()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync("/api/v1/trust/reputation/rules");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.Private.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().Be(TimeSpan.FromHours(1));
    }

    [Fact]
    public async Task The_rules_need_a_signed_in_member()
    {
        Api.ClearAuthentication();

        var response = await HttpClient.GetAsync("/api/v1/trust/reputation/rules");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private sealed record RuleView(
        string ActionType,
        string Label,
        string Dimension,
        int Points,
        int? MaxOccurrences,
        int? CooldownHours,
        bool IsCountryScoped,
        int? CreditPoints = null);
}
