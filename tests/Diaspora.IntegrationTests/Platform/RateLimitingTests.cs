using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Contracts.Identity.V1.Requests;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Diaspora.IntegrationTests.Platform;

/// <summary>
/// Rate limiting, exercised in isolation with tight budgets.
/// <para>
/// The main suites run with the limiter off — they drive dozens of sign-ins a second, the exact
/// shape the limiter refuses. These turn it on with budgets of two and confirm that the limiter
/// actually rejects, and does so as the platform's problem+json rather than a bare 429.
/// </para>
/// <para>
/// <b>No token anywhere in this class, deliberately.</b> <c>UseRateLimiter</c> runs before
/// <c>UseAuthentication</c>, so a refusal costs neither a signature validation nor a database
/// read. A 429 arriving on an unauthenticated request to an authenticated endpoint is therefore
/// the behaviour under test rather than a gap in the test.
/// </para>
/// </summary>
public sealed class RateLimitingTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly CustomWebApplicationFactory _factory;

    public RateLimitingTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        factory.EnsureDatabaseCreated();
    }

    [Fact]
    public async Task Sign_in_is_throttled_once_the_budget_is_spent()
    {
        // A fresh host with the limiter on and a budget of two sign-ins per window. The
        // override is layered after the base factory's config, so it wins.
        using var throttled = Throttled(
            ("RateLimiting:SignIn:PermitLimit", "2"),
            ("RateLimiting:SignIn:WindowSeconds", "60"));

        using var client = throttled.CreateClient();

        var request = new LoginRequest { Email = "member2348000000001@diaspora.test", Password = "whatever-wrong" };

        // The first two are allowed through (and fail auth, which is fine — the limiter, not
        // the credentials, is under test). The third is refused by the limiter.
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
        {
            var response = await client.PostAsJsonAsync("/api/v1/auth/login", request, JsonOptions);
            statuses.Add(response.StatusCode);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var body = await response.Content.ReadAsStringAsync();
                body.Should().Contain("Platform.RateLimit.Exceeded");
                response.Headers.Should().ContainKey("Retry-After");
            }
        }

        statuses.Should().Contain(HttpStatusCode.TooManyRequests);
    }

    /// <summary>
    /// <b>BSEC-002's first verification criterion: a financial endpoint is bounded.</b>
    /// <para>
    /// The point is not the number — the budget is forced down to two so the test takes a second
    /// rather than a minute — but <i>where the budget came from</i>. The Financial module's 114
    /// routes name no rate-limit policy, and nobody edited them: this 429 exists because the
    /// composition root gives an endpoint a tier whether it asked for one or not. If that default
    /// were removed, this request would succeed and no other test in the repository would fail.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_financial_endpoint_that_never_asked_to_be_throttled_is_throttled()
    {
        using var throttled = Throttled(
            ("RateLimiting:Anonymous:PermitLimit", "2"),
            ("RateLimiting:Anonymous:WindowSeconds", "60"));

        using var client = throttled.CreateClient();

        var statuses = await Hammer(client, HttpMethod.Get, "/api/v1/financial/wallets/me", 4);

        statuses.Should().Contain(
            HttpStatusCode.TooManyRequests,
            "every Financial endpoint was unthrottled, and the module still names no policy — "
            + "the bound comes from the default applied where modules are composed");
    }

    /// <summary>
    /// <b>BSEC-002's second verification criterion: an anonymous search is refused.</b>
    /// <para>
    /// <c>GET /api/v1/guides</c> takes filters and a page size from a caller who has proved
    /// nothing and answers them from the database. It is the finding's own example of the surface
    /// where an unbounded request is a cost event as much as an availability one, and it draws on
    /// the search tier rather than the anonymous one — which this proves by exhausting only the
    /// search budget.
    /// </para>
    /// </summary>
    [Fact]
    public async Task An_anonymous_search_is_refused_once_the_search_budget_is_spent()
    {
        using var throttled = Throttled(
            ("RateLimiting:Search:PermitLimit", "2"),
            ("RateLimiting:Search:WindowSeconds", "60"));

        using var client = throttled.CreateClient();

        var statuses = await Hammer(client, HttpMethod.Get, "/api/v1/guides", 4);

        statuses.Should().Contain(
            HttpStatusCode.TooManyRequests,
            "the search tier is what bounds an anonymous, database-backed, caller-parameterised "
            + "read");

        // Only the search budget was spent. GET / is on the anonymous tier at its full configured
        // budget, so it must still answer — ten times over.
        //
        // This is the assertion that makes the tiers mean something. One shared bucket would fail
        // here, and a shared bucket is not a hypothetical: three module groups were pointed at the
        // sign-in budget, and /bookmarks was refused on its first request because a conversation
        // list had already spent it. An exempt endpoint would have answered here for a reason that
        // has nothing to do with tiers, so this uses a throttled one.
        var unrelated = await Hammer(client, HttpMethod.Get, "/", 10);

        unrelated.Should().NotContain(
            HttpStatusCode.TooManyRequests,
            "spending the search budget must not spend the anonymous one");
    }

    /// <summary>
    /// <b>The health probe keeps answering when every budget is spent.</b> The two exemptions
    /// exist for this: the limiter partitions on the caller's address, and the orchestrator's
    /// probe arrives through the same ingress as the flood. A throttled probe would stop answering
    /// during exactly the traffic spike it exists to survive, the machine would be replaced, and
    /// the replacement would meet the same spike — a load event turned into a restart loop by the
    /// control meant to contain it.
    /// </summary>
    [Fact]
    public async Task The_liveness_probe_answers_while_every_other_budget_is_spent()
    {
        using var throttled = Throttled(
            ("RateLimiting:Anonymous:PermitLimit", "1"),
            ("RateLimiting:Authenticated:PermitLimit", "1"),
            ("RateLimiting:Search:PermitLimit", "1"),
            ("RateLimiting:Webhook:PermitLimit", "1"),
            ("RateLimiting:Upload:PermitLimit", "1"));

        using var client = throttled.CreateClient();

        var spent = await Hammer(client, HttpMethod.Get, "/api/v1/financial/wallets/me", 3);
        spent.Should().Contain(
            HttpStatusCode.TooManyRequests, "the budget has to be genuinely exhausted first, or "
            + "this test proves nothing about the exemption");

        // Twenty probes against a budget of one. An orchestrator checks far more often than this.
        var probes = await Hammer(client, HttpMethod.Get, "/health/live", 20);

        probes.Should().NotContain(
            HttpStatusCode.TooManyRequests,
            "the liveness probe is exempt, and the whole point of the exemption is that it holds "
            + "while everything else is refused");
    }

    private static async Task<List<HttpStatusCode>> Hammer(
        HttpClient client, HttpMethod method, string path, int times)
    {
        var statuses = new List<HttpStatusCode>();

        for (var i = 0; i < times; i++)
        {
            using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
            using var response = await client.SendAsync(request);
            statuses.Add(response.StatusCode);
        }

        return statuses;
    }

    /// <summary>
    /// A host with the limiter on and the given budgets. Layered after the base factory's
    /// configuration, so these win.
    /// </summary>
    private WebApplicationFactory<Api.Program> Throttled(params (string Key, string Value)[] settings)
    {
        var overrides = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["RateLimiting:Enabled"] = "true"
        };

        foreach (var (key, value) in settings)
        {
            overrides[key] = value;
        }

        return _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(overrides)));
    }
}
