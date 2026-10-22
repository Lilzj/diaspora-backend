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
            [
                ReputationActions.UpdateVerified,
                ReputationActions.ReportResolved,
                ReputationActions.ProfilePhotoAdded,
                ReputationActions.ProfileCompleted
            ],
            "nothing pays them, and the list is read as promises");

        rules.Should().ContainEquivalentOf(new RuleView(
            ReputationActions.GuidePublished, "Guides published", "contribution", 50, null, 1, true));
        rules.Should().ContainEquivalentOf(new RuleView(
            ReputationActions.CountryVerified, "Countries verified", "verification", 30, 1, null, true));
        rules.Should().ContainEquivalentOf(new RuleView(
            ReputationActions.ViolationRecorded, "Policy violations", "trust", -75, null, null, false));
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
        bool IsCountryScoped);
}
