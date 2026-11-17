using System.Net;
using System.Text.Json;
using Diaspora.Api.Middleware;
using Diaspora.Domain.Abstractions;
using Diaspora.Platform.Http;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Diaspora.ApiTests;

/// <summary>
/// BBL-002, at the boundary. A duplicate and a lost update are both 409 and must not look alike on
/// the wire, because the client's next move is opposite in each case.
/// <para>
/// The translation these responses depend on is proved separately, against a real database, in
/// <c>Diaspora.UnitTests.Persistence.DuplicateRecordTranslationTests</c>. What is proved here is
/// the half that faces the caller: the status, the code they switch on, and that the two 409s stay
/// distinguishable.
/// </para>
/// </summary>
public sealed class DuplicateResponseTests
{
    [Fact]
    public async Task A_duplicate_is_a_conflict_the_caller_should_not_retry()
    {
        using var host = await HostRefusing(
            new DuplicateRecordException("A record with those details already exists."));

        var response = await host.GetTestClient().GetAsync(new Uri("/anything", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType!.MediaType.Should().Be(PlatformProblem.ContentType);

        var problem = await ProblemAsync(response);

        problem.GetProperty("code").GetString().Should().Be(PlatformErrorCodes.Duplicate);
        problem.GetProperty("title").GetString().Should().Be("Already exists");
        problem.GetProperty("detail").GetString()
            .Should().Be("A record with those details already exists.");
    }

    /// <summary>
    /// <b>The distinction the new code exists for.</b> Same status, different instruction: reload
    /// and try again for a lost update, and do not bother for a duplicate. A client that cannot
    /// tell them apart has to guess, and before this both were
    /// <c>500 Platform.Unexpected "An unexpected error occurred."</c> anyway.
    /// </summary>
    [Fact]
    public async Task A_lost_update_says_something_different()
    {
        using var host = await HostRefusing(new ConcurrencyConflictException());

        var response = await host.GetTestClient().GetAsync(new Uri("/anything", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await ProblemAsync(response);

        problem.GetProperty("code").GetString()
            .Should().Be(PlatformErrorCodes.ConcurrencyConflict)
            .And.NotBe(PlatformErrorCodes.Duplicate);

        problem.GetProperty("detail").GetString().Should().Contain("Reload and try again");
    }

    /// <summary>
    /// The arm is narrow. Anything the platform did not recognise is still a server fault, which is
    /// what makes a 409 here mean something.
    /// </summary>
    [Fact]
    public async Task Something_genuinely_unexpected_is_still_a_server_error()
    {
        using var host = await HostRefusing(new InvalidOperationException("the disk melted"));

        var response = await host.GetTestClient().GetAsync(new Uri("/anything", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        var problem = await ProblemAsync(response);

        problem.GetProperty("code").GetString().Should().Be(PlatformErrorCodes.Unexpected);
    }

    /// <summary>
    /// The middleware and nothing else: no routing, no module, no database. A host that carried the
    /// whole pipeline would prove that some handler happens to throw this, which is a different
    /// claim from the one these tests make.
    /// </summary>
    private static async Task<IHost> HostRefusing(Exception refusal)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .UseEnvironment(Environments.Production)
                .Configure(app =>
                {
                    app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
                    app.Run(_ => throw refusal);
                }))
            .StartAsync();

        return host;
    }

    private static async Task<JsonElement> ProblemAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return document.RootElement.Clone();
    }
}
