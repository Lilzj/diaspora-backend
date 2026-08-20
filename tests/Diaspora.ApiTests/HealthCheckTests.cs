using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Diaspora.ApiTests;

/// <summary>
/// Tests the two health endpoints against the real host with no database behind it.
/// <para>
/// That "no database" is the whole point. B2's readiness check returned 200 unconditionally,
/// so this suite could not tell a healthy instance from one that could not reach its
/// database. B2.6 made readiness genuinely probe the connection, so against this host —
/// which has no reachable PostgreSQL — readiness must report 503 while liveness, which
/// depends on nothing external, stays 200. A green readiness here would mean the check was
/// fake again.
/// </para>
/// </summary>
public class HealthCheckTests : IClassFixture<WebApplicationFactory<Api.Program>>
{
    private readonly HttpClient _client;

    public HealthCheckTests(WebApplicationFactory<Api.Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_live_endpoint_is_healthy_without_any_dependency()
    {
        var response = await _client.GetAsync(new Uri("/health/live", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Health_ready_endpoint_reports_unhealthy_when_the_database_is_unreachable()
    {
        var response = await _client.GetAsync(new Uri("/health/ready", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        // The body names which dependency failed rather than saying only "Unhealthy".
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("database");
    }

    [Fact]
    public async Task Root_endpoint_should_return_platform_info()
    {
        var response = await _client.GetAsync(new Uri("/", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Diaspora");
    }
}
