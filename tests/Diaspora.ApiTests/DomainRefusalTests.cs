using System.Net;
using System.Text.Json;
using Diaspora.Api.Middleware;
using Diaspora.Domain.Abstractions;
using Diaspora.Platform.Messaging.Outbox.Model;
using Diaspora.Platform.Messaging.Outbox.Options;
using Diaspora.Platform.Messaging.Outbox.Retry;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diaspora.ApiTests;

/// <summary>
/// What the platform does when a domain invariant refuses — BBL-001.
/// <para>
/// <b>It used to depend on nobody forgetting.</b> <see cref="DomainException"/> is thrown in 108
/// places across Financial, Trust and Recommendations and was mapped at neither boundary. The
/// careful modules caught it themselves — six Financial handlers return
/// <c>Error.Conflict(exception.Code, exception.Message)</c>, which is where the 409 below comes
/// from — so the gap never produced a visible failure and was never noticed. The defect was not
/// that business refusals returned 500; it was that nothing made that untrue.
/// </para>
/// <para>
/// <b>Two boundaries, because a refusal can escape in two directions.</b> A request reaches
/// <see cref="GlobalExceptionHandlingMiddleware"/>. An integration event does not: it is consumed
/// out of band by the outbox dispatcher, where the question is not what status to return but
/// whether retrying could ever help. Both are covered here, in one file, because they are one
/// finding and the second is the one that is easy to believe is handled when it is not.
/// </para>
/// <para>
/// <b>The audit proposed a MediatR pipeline behaviour for the second half, and that could not have
/// worked.</b> Its stated purpose was to reach the consumer case the HTTP middleware cannot, but
/// <c>IntegrationEventHandler&lt;T&gt;</c> implements <c>INotificationHandler</c>, and MediatR runs
/// <c>IPipelineBehavior</c> only on <c>ISender.Send</c>. The platform proves this itself:
/// <c>AuthorizationBehavior</c> refuses any request type carrying no authorization attribute, and
/// <c>IntegrationEventNotification&lt;T&gt;</c> carries none — were behaviours running on
/// notifications, every consumer on the platform would be refused. The seam that does reach the
/// consumer is <c>PoisonMessageDetector</c>, and it was already built for exactly this.
/// </para>
/// <para>
/// Deliberately not the Diaspora host, following <see cref="ForwardedHeaderTrustTests"/>: the
/// first three assertions are about one middleware's behaviour and booting every module would
/// prove the same thing far more slowly.
/// </para>
/// </summary>
public sealed class DomainRefusalTests
{
    private const string Code = "financial.wallet.has_held_funds";

    private const string Message = "This wallet still holds funds that have not settled.";

    // ── The request boundary ────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>A domain refusal is a 409 carrying the domain's own code</b>, not
    /// <c>500 Platform.Unexpected</c>.
    /// </summary>
    /// <remarks>
    /// The code is the whole point. A client's error vocabulary is built on it, and the catch-all
    /// discarded it along with the message — a member who had just moved money was told an
    /// unexpected error occurred, which does not say whether it happened.
    /// </remarks>
    [Fact]
    public async Task A_domain_refusal_is_a_conflict_carrying_its_own_code()
    {
        var problem = await RefuseWith(new DomainException(Message, Code));

        problem.Status.Should().Be(HttpStatusCode.Conflict);
        problem.Code.Should().Be(Code);
        problem.Detail.Should().Be(Message);
    }

    /// <summary>
    /// <b>The default code still names the family it came from.</b>
    /// </summary>
    /// <remarks>
    /// <c>DomainException</c>'s code parameter is optional and defaults to <c>Domain.Error</c>, so
    /// a throw site that names no code is still distinguishable from a server fault. Asserted
    /// because the alternative — the arm reading an empty code and emitting one — would be a
    /// problem document with no usable discriminator at all.
    /// </remarks>
    [Fact]
    public async Task A_refusal_that_names_no_code_still_names_its_family()
    {
        var problem = await RefuseWith(new DomainException(Message));

        problem.Status.Should().Be(HttpStatusCode.Conflict);
        problem.Code.Should().Be("Domain.Error");
    }

    /// <summary>
    /// <b>And something genuinely unexpected is still a 500.</b>
    /// </summary>
    /// <remarks>
    /// The arm above is only worth having if it is narrow. A mapping that turned every escaped
    /// exception into a 409 would be strictly worse than the catch-all it replaced: it would tell
    /// the caller their request was refused when the server had in fact broken, and a 409 does not
    /// page anybody.
    /// </remarks>
    [Fact]
    public async Task Something_genuinely_unexpected_is_still_a_server_error()
    {
        var problem = await RefuseWith(new InvalidOperationException("a real fault"));

        problem.Status.Should().Be(HttpStatusCode.InternalServerError);
        problem.Code.Should().NotBe(Code);
    }

    // ── The consumer boundary ───────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The shipped configuration makes a domain refusal poison</b>, so a consumer that trips an
    /// invariant dead-letters at once instead of after eight attempts over forty minutes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bound from the real <c>appsettings.json</c> rather than from a hand-built options object,
    /// because the claim being made is about what this platform ships, not about what the policy
    /// is capable of — <c>ExponentialBackoffRetryPolicy</c> already has its own suite proving a
    /// configured type is honoured. What was missing is that the platform configures one.
    /// </para>
    /// <para>
    /// Throwing a real <see cref="DomainException"/> is also what binds the configured string to
    /// the type. <c>PoisonMessageDetector</c> matches on the type's name, so renaming
    /// <c>DomainException</c> would silently stop classifying it and nothing would say so; this
    /// fails instead.
    /// </para>
    /// <para>
    /// <b>Why dead-lettering sooner is the right answer rather than swallowing.</b>
    /// <c>FinancialAppealApprovedConsumer</c> calls <c>wallet.Activate</c>, which refuses on a
    /// closed wallet. Recording that as success would mean an approved appeal silently never
    /// reaches the wallet. The reason code is what changes: <c>MaxAttemptsExceeded</c> tells an
    /// operator a dependency was probably down and the message is worth replaying, which is the
    /// wrong instruction for a refusal that will refuse again.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_shipped_configuration_makes_a_domain_refusal_poison()
    {
        var decision = ShippedRetryPolicy().Decide(
            new OutboxFailure(0, new DomainException(Message, Code), DateTimeOffset.UtcNow, "appeal.approved"));

        decision.Action.Should().Be(
            OutboxRetryAction.DeadLetter,
            "a domain invariant refuses on the state in the database, so the next seven attempts "
            + "refuse identically");

        decision.Reason.Should().Be(
            OutboxDeadLetterReason.Poison,
            "MaxAttemptsExceeded tells an operator the message is worth replaying, and this one "
            + "is not until somebody changes the state it was refused on");
    }

    /// <summary>
    /// <b>And a transient failure is still retried</b>, which is what makes the arm above narrow.
    /// </summary>
    /// <remarks>
    /// <c>PoisonMessageDetector</c>'s own documentation calls itself deliberately conservative:
    /// the cost of misclassifying a transient failure as poison is a message given up on while the
    /// thing that would have delivered it was thirty seconds from returning. Adding an entry to
    /// that list is the kind of change that could quietly widen it, so this asserts it did not.
    /// </remarks>
    [Fact]
    public void A_transient_consumer_failure_is_still_retried()
    {
        var decision = ShippedRetryPolicy().Decide(
            new OutboxFailure(0, new TimeoutException("the consumer was slow"), DateTimeOffset.UtcNow, "appeal.approved"));

        decision.Action.Should().Be(OutboxRetryAction.Retry);
    }

    // ── Harness ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The retry policy built from the configuration this host actually ships.
    /// </summary>
    private static ExponentialBackoffRetryPolicy ShippedRetryPolicy()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var options = new OutboxOptions();
        configuration.GetSection("Platform:Outbox").Bind(options);

        options.Retry.PoisonExceptionTypes.Should().NotBeEmpty(
            "this test is about what the shipped configuration declares, and an empty list would "
            + "make the assertions below pass or fail for reasons that have nothing to do with it");

        return new ExponentialBackoffRetryPolicy(Options.Create(options));
    }

    /// <summary>
    /// Runs a pipeline carrying only the exception middleware, over a terminal that throws.
    /// </summary>
    private static async Task<(HttpStatusCode Status, string? Code, string? Detail)> RefuseWith(
        Exception thrown)
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services => services.AddLogging())
                .Configure(app =>
                {
                    app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
                    app.Run(_ => throw thrown);
                }))
            .StartAsync();

        using var client = host.GetTestServer().CreateClient();
        using var response = await client.GetAsync(new Uri("/", UriKind.Relative));

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);

        return (
            response.StatusCode,
            Read(document, "code"),
            Read(document, "detail"));
    }

    private static string? Read(JsonDocument document, string property) =>
        document.RootElement.TryGetProperty(property, out var value) ? value.GetString() : null;
}
