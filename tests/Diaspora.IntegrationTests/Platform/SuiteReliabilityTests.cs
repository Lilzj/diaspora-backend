using System.Net;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Platform;

/// <summary>
/// BARCH-009. The suite's own reliability, asserted by the suite.
/// <para>
/// <b>The flakiness was a number, and the number was 100.</b> Across three contended runs,
/// 23 failures were named: every one a <c>TaskCanceledException</c> rather than an assertion, and
/// every one on a test that had been running for longer than 100 seconds — which is exactly
/// <c>HttpClient.Timeout</c>'s default. The clearest single data point is the one failure in an
/// otherwise idle run, which took 1 minute 43 seconds: 103 seconds.
/// </para>
/// <para>
/// <b>The cause is upstream of the timeout and is already documented in this suite.</b> Building one
/// application costs 32 seconds (measured 23 September 2026, recorded on
/// <see cref="HousingCollection"/>); 61 of 205 test classes still build one of their own; and xUnit
/// builds as many at once as the machine has processors. The timeout did not cause the contention,
/// it converted it into failures — and 21 of those 23 named failures sat in the 30% of classes that
/// build their own host, which is a threefold enrichment.
/// </para>
/// <para>
/// These tests pin the three properties that follow from that, because each of them is the kind of
/// default that gets restored by accident.
/// </para>
/// </summary>
[Collection(PlatformCollection.Name)]
public sealed class SuiteReliabilityTests
{
    private readonly CustomWebApplicationFactory _factory;

    public SuiteReliabilityTests(CustomWebApplicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    /// <summary>
    /// <b>No client this suite uses may impose a wall-clock deadline.</b> A test client is not a
    /// user and not a service-level objective, and any finite number here is a guess about machine
    /// speed that the whole finding is the consequence of getting wrong.
    /// </summary>
    [Fact]
    public void A_test_client_imposes_no_deadline_of_its_own()
    {
        using var client = _factory.CreateClient();

        client.Timeout.Should().Be(
            Timeout.InfiniteTimeSpan,
            "100 seconds — the default this replaces — is what turned contention into 23 named "
            + "TaskCanceledExceptions across three runs");
    }

    /// <summary>
    /// And the same for a client built through <c>WithWebHostBuilder</c>, which is how the tests
    /// that need their own configuration get one. It produces a <em>different</em> factory
    /// instance, so a fix applied only to the outer one would leave exactly the heaviest tests —
    /// the ones that build extra hosts — still on the default.
    /// <para>
    /// <b>On a factory of its own, and that is not a detail.</b> Written against this collection's
    /// shared factory, this test destroyed twenty tests in four other classes. <c>WithWebHostBuilder</c>
    /// builds a derived host over the parent's <em>same</em> connection field, and disposing the
    /// derived factory closes it — which, for the in-memory SQLite database this suite runs on,
    /// is the sentence at the top of <see cref="CustomWebApplicationFactory"/>: the database is
    /// destroyed the moment its last connection closes. Every class scheduled after this one in the
    /// collection then failed in milliseconds against a database that no longer existed.
    /// </para>
    /// <para>
    /// So the derived host is built over a factory this test owns and nobody else shares. Every
    /// other <c>WithWebHostBuilder</c> caller in the suite is in a class with no
    /// <c>[Collection]</c> — a convention that held by habit until this test broke it, and that
    /// <see cref="SuiteConventionTests"/> now states.
    /// </para>
    /// </summary>
    [Fact]
    public void So_does_a_client_from_a_reconfigured_host()
    {
        using var standalone = new CustomWebApplicationFactory();
        using var reconfigured = standalone.WithWebHostBuilder(_ => { });
        using var client = reconfigured.CreateClient();

        client.Timeout.Should().Be(Timeout.InfiniteTimeSpan);
    }

    /// <summary>
    /// <b>Nothing here is reached through a redirect, and that is thinner than it looks.</b>
    /// <c>Program.cs</c> mounts <c>UseHttpsRedirection</c> for every environment that is not
    /// Development, and this suite runs in Testing — so the middleware is in the pipeline. It is
    /// inert only because <c>HttpsRedirectionMiddleware</c> cannot discover an https port under
    /// <c>TestServer</c> and skips rather than redirecting.
    /// <para>
    /// That is the load-bearing assumption behind turning <c>AllowAutoRedirect</c> off. If an https
    /// port ever becomes discoverable in the test host, every request in the suite starts answering
    /// <c>307</c> and this test is the one that says why.
    /// </para>
    /// </summary>
    [Fact]
    public async Task No_request_in_this_suite_is_answered_with_a_redirect()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri("/", UriKind.Relative));

        ((int)response.StatusCode).Should().BeLessThan(
            300,
            "UseHttpsRedirection is mounted in Testing and is inert only for want of a "
            + "discoverable https port. Status was {0}",
            response.StatusCode);

        response.Headers.Location.Should().BeNull();
    }

    /// <summary>
    /// <b>The runner configuration has to be beside the assembly to do anything.</b> Read from the
    /// output directory rather than from the source tree, because a <c>xunit.runner.json</c> that is
    /// present in the repository and not copied is the worst of both: the setting looks made and no
    /// long-running test is ever reported.
    /// <para>
    /// This is the replacement for the deadline the clients no longer impose, so it is the one thing
    /// standing between a genuinely hung test and a CI job that dies at its 45-minute budget with no
    /// test named.
    /// </para>
    /// </summary>
    [Fact]
    public void The_runner_reports_a_test_that_stops_making_progress()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "xunit.runner.json");

        File.Exists(path).Should().BeTrue(
            "a runner config that is not deployed beside the assembly is not a runner config");

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;

        root.TryGetProperty("longRunningTestSeconds", out var seconds).Should().BeTrue();

        seconds.GetInt32().Should().BeInRange(
            100,
            300,
            "above the 100-second default it replaces, so nothing that used to fail now passes "
            + "unreported, and below the point where a hang would outlive the CI budget");

        root.TryGetProperty("diagnosticMessages", out var diagnostics).Should().BeTrue(
            "longRunningTestSeconds reaches the output only with diagnostic messages on");

        diagnostics.GetBoolean().Should().BeTrue();
    }
}
