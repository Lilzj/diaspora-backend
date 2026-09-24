using System.Net;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Platform;

/// <summary>
/// Readiness against a host with a reachable database. The complement of the ApiTests
/// suite, which runs with no database and asserts readiness reports 503 — together they
/// prove the check reflects the real state rather than always answering the same way, which
/// is exactly what B2's placeholder did.
/// </summary>
[Collection(PlatformCollection.Name)]
public sealed class HealthCheckTests : PooledIdentityTestBase
{
    public HealthCheckTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Liveness_is_healthy()
    {
        var response = await HttpClient.GetAsync(new Uri("/health/live", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Readiness_is_healthy_when_the_database_and_modules_are_up()
    {
        var response = await HttpClient.GetAsync(new Uri("/health/ready", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("database");
        body.Should().Contain("modules");
    }

    [Fact]
    public async Task Readiness_reports_the_module_catalogue_composed()
    {
        var response = await HttpClient.GetAsync(new Uri("/health/ready", UriKind.Relative));
        var body = await response.Content.ReadAsStringAsync();

        // The module readiness check resolves the catalogues; a composition failure would
        // surface here as an unhealthy modules entry rather than as 500s on first request.
        body.Should().Contain("Healthy");
    }
}
