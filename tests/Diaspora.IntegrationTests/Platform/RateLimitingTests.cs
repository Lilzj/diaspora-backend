using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Contracts.Identity.V1.Requests;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Diaspora.IntegrationTests.Platform;

/// <summary>
/// Rate limiting, exercised in isolation with a tight sign-in budget.
/// <para>
/// The main identity suite runs with the limiter off — it drives dozens of sign-ins a
/// second, the exact shape the limiter refuses. This suite turns it on with a budget of two
/// and confirms the transport limiter actually rejects, and does so as the platform's
/// problem+json rather than a bare 429.
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
        using var throttled = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimiting:Enabled"] = "true",
                    ["RateLimiting:SignIn:PermitLimit"] = "2",
                    ["RateLimiting:SignIn:WindowSeconds"] = "60"
                })));

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
}
