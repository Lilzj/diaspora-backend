using System.Net.Http.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Performance;

/// <summary>
/// What a request costs the database, counted where the request is served.
/// <para>
/// <b>BBL-005 asks for the feed's bound at the endpoint, and that is a different number from the
/// one the first half of this finding asserted.</b> The repository budget answers 1, because
/// inside a single repository call there is nothing else to spend a query on. A request spends
/// queries on the viewer's context, authorization, the feed provider, hydration and the trust
/// levels — and the loop the finding was about lived in exactly that space between them.
/// </para>
/// <para>
/// <b>The load-bearing assertion is the shape, not the number.</b> A fixed ceiling fails the day
/// somebody adds a legitimate query and teaches the next person to raise it; what must never
/// change is that the cost does not grow with the page. So the first test below puts four more
/// authors into the feed and requires the count to be identical, which is the property an N+1
/// breaks and a refactor does not.
/// </para>
/// <para>
/// Its own host, because the counter is a singleton on it and a shared collection's traffic would
/// land in the same count.
/// </para>
/// </summary>
public sealed class EndpointQueryBudgetTests : IdentityTestBase<QueryCountingWebApplicationFactory>
{
    private const string HomeFeed = "/api/v1/community/feeds/home";

    public EndpointQueryBudgetTests(QueryCountingWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task A_feed_page_costs_the_same_whether_it_holds_one_author_or_five()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);
        await PostAsync("The 345 is running late again.");

        var withOneAuthor = await MeasureAsync(HomeFeed);

        for (var author = 0; author < 4; author++)
        {
            var (_, other) = await RegisterVerifiedMemberAsync();
            Api.Authenticate(other.Tokens.AccessToken);
            await PostAsync($"Another author, number {author}.");
        }

        Api.Authenticate(auth.Tokens.AccessToken);

        var withFiveAuthors = await MeasureAsync(HomeFeed);

        withFiveAuthors.Should().Be(
            withOneAuthor,
            "a feed page must cost the same whatever it holds — one round trip per distinct " +
            "author is the defect this finding is about, and it is invisible until the page is " +
            "full. Statements:\n{0}",
            Factory.Counter.Describe());
    }

    [Fact]
    public async Task A_feed_request_stays_inside_its_budget()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);
        await PostAsync("Anybody know when the library reopens?");

        var cost = await MeasureAsync(HomeFeed);

        cost.Should().BeLessThanOrEqualTo(
            FeedCeiling,
            "the endpoint's whole cost is budgeted, not just its repository's. Raising this " +
            "number is a decision somebody should have to make on purpose. Statements:\n{0}",
            Factory.Counter.Describe());
    }

    /// <summary>
    /// <b>BARCH-005's second ask: a budget on discovery search.</b>
    /// <para>
    /// The event search is the path with the only bounded loop left on the platform — Nearest
    /// reads up to five pages of candidates because the database has no distance to sort by, and
    /// hydrates each. That is correct and documented, and it is also the shape that quietly
    /// becomes expensive if the bound moves, which is why it is worth a number.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_discovery_search_stays_inside_its_budget()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var cost = await MeasureAsync("/api/v1/community-life/events?limit=20");

        cost.Should().BeLessThanOrEqualTo(
            SearchCeiling,
            "discovery search is budgeted too, and the Nearest path's five-page candidate walk is " +
            "the reason. Statements:\n{0}",
            Factory.Counter.Describe());
    }

    /// <summary>
    /// <b>A budget is an assertion that a number is small, which a counter that counts nothing
    /// satisfies forever.</b> This one reads the opposite: two requests cost more than one. If the
    /// budgets above ever pass because the interceptor went blind, this fails.
    /// </summary>
    [Fact]
    public async Task The_counter_is_counting()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var one = await MeasureAsync(HomeFeed);

        Factory.Counter.Reset();
        (await HttpClient.GetAsync(HomeFeed)).EnsureSuccessStatusCode();
        (await HttpClient.GetAsync(HomeFeed)).EnsureSuccessStatusCode();
        var two = Factory.Counter.Count;

        one.Should().BeGreaterThan(
            0, "serving a feed reads from the database, so a count of zero means the interceptor " +
               "is not attached and every budget here is vacuous");

        two.Should().BeGreaterThan(
            one, "two requests cost more than one; a counter that reports otherwise is not " +
                 "counting");
    }

    /// <summary>
    /// The ceiling, measured rather than guessed: a home feed request cost <b>20</b> commands on
    /// 22 November 2026 — the viewer's context, the access state, authorization, the feed
    /// provider's page, hydration, the trust levels and the engagement counts.
    /// <para>
    /// Deliberately not tight. A ceiling that sits one above the measurement fails on the next
    /// legitimate query and teaches whoever is unblocking the build to raise it, which is how a
    /// budget becomes a number nobody reads. The assertion that carries the finding is the one
    /// above, which does not move with the page at all.
    /// </para>
    /// </summary>
    private const int FeedCeiling = 24;

    /// <summary>
    /// Measured on 22 November 2026: an event search cost <b>4</b> commands — the viewer, the
    /// access state and the search itself. Headroom for the same reason as the feed's.
    /// </summary>
    private const int SearchCeiling = 8;

    private async Task<int> MeasureAsync(string route)
    {
        Factory.Counter.Reset();

        var response = await HttpClient.GetAsync(route);

        response.EnsureSuccessStatusCode();

        return Factory.Counter.Count;
    }

    private async Task PostAsync(string body)
    {
        var response = await HttpClient.PostAsJsonAsync(
            "/api/v1/community/posts",
            new { body, category = "Transportation" });

        response.EnsureSuccessStatusCode();
    }
}
