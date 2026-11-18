using Diaspora.Platform.Modules;
using Diaspora.Platform.Operations.Policies;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// Every call that leaves the process is governed by a policy somebody can read.
/// <para>
/// <b>B17-R4 already forbids the wrong kind of resilience and says nothing about its absence.</b>
/// <c>No_module_writes_its_own_retry_loop</c> fails a build that hand-rolls a retry, which is the
/// right rule — and it scans <c>src/Diaspora.Modules.*</c> only, so it has never looked at an
/// adapter, and the outbound calls live in the adapters. Between the two gaps, the platform could
/// not tell the difference between a client under the breaker and a client with nothing but a
/// timeout.
/// </para>
/// <para>
/// <b>That difference was not hypothetical.</b> BARCH-007 is recorded as DESIGN_RISK because the
/// audit looked for Polly and found none; the platform in fact has a first-class breaker, an
/// operator dashboard for it and a declared payment policy that is the strictest on the platform.
/// What it did not have was a caller: before the change these rules arrived with,
/// <c>financial.payment-provider</c> appeared in exactly two places in the repository — where the
/// constant is declared, and where the policy is declared.
/// </para>
/// </summary>
public sealed class OutboundResilienceArchitectureTests
{
    private const string Governs = "WithPlatformResilience";

    /// <summary>
    /// A client that is not routed through <see cref="Governs"/>, and why that is allowed.
    /// </summary>
    /// <param name="Client">
    /// The registration's first argument, verbatim — <c>GoogleRoutingProvider.HttpClientName</c>.
    /// </param>
    /// <param name="Why">
    /// <b>Two different things live in this column and keeping them apart is the point.</b> A client
    /// governed at its call site is finished work: the executor runs it, the breaker sees it, and a
    /// handler would be a second wrapper around the same thing. A client governed by nothing is not
    /// finished, and writing that down is how it stops being invisible.
    /// </param>
    private sealed record UngovernedClient(string Client, string Why);

    /// <summary>
    /// The clients that do not carry the handler, measured 18 November 2026.
    /// <para>
    /// Seeded from the scan below rather than by hand, then given a reason each. The list may
    /// shrink; an addition to it is a decision about an outbound call, which is the point.
    /// </para>
    /// </summary>
    private static readonly UngovernedClient[] Ungoverned =
    [
        new("ResendEmailGateway.HttpClientName",
            "Governed at the call site: GatewayEmailSender runs every send through " +
            "IResilienceExecutor under email.resend, with its own retry, breaker and classifier."),

        new("IntegrationHttpGateway.HttpClientName",
            "Governed at the call site: IntegrationHttpGateway is the executor seam for every " +
            "outbound integration call, which is what B17-R4 exists to keep it."),

        new("TwilioSmsGateway.HttpClientName",
            "Governed at the call site: RoutedSmsOtpDeliveryService resolves the dependency per " +
            "provider — sms.twilio — and runs the dispatch through IResilienceExecutor. The " +
            "per-provider name is the point: one gateway's failures must not break the other, " +
            "because the router's whole job is to fall through to it."),

        new("AfricasTalkingSmsGateway.HttpClientName",
            "Governed at the call site under sms.africas-talking. Same as the Twilio client."),

        new("GoogleRoutingProvider.HttpClientName",
            "NOT GOVERNED. Backpressure is a monthly quota in platform.provider_usage, which " +
            "limits spend rather than concurrency; a slow Maps call still holds its connection."),

        new("GooglePlaceSearchProvider.HttpClientName",
            "NOT GOVERNED. Same as the routing client."),

        new("PoliceUkCrimeData.HttpClientName",
            "NOT GOVERNED. A public dataset read on a background path, so a stall delays an " +
            "intelligence refresh rather than a member's request."),

        new("HttpTrustDecisionPublisher.HttpClientName",
            "NOT GOVERNED. Publishes a decision outward; a stall holds a background dispatch.")
    ];

    // ── The rules ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The rule.</b> A named <c>HttpClient</c> either carries the platform's resilience handler
    /// or is on the list above with a reason. There is no third state, because the third state is
    /// what BARCH-007 found: eleven payment clients with a transport timeout and nothing else.
    /// </summary>
    [Fact]
    public void Every_outbound_client_is_governed_or_declared()
    {
        var declared = Ungoverned.Select(entry => entry.Client).ToHashSet(StringComparer.Ordinal);

        var offenders = Registrations()
            .Where(registration => !registration.Governed)
            .Where(registration => !declared.Contains(registration.Client))
            .Select(registration =>
                $"{registration.Where}: {registration.Client} leaves the process with no " +
                "resilience policy. Chain .WithPlatformResilience(dependency) onto the " +
                "registration, or add it to Ungoverned with the reason it cannot be.")
            .Order(StringComparer.Ordinal)
            .ToList();

        offenders.Should().BeEmpty(
            "a call with no breaker is a thread and a connection held for the full timeout, and " +
            "on a single instance that is the whole pool:\n" + string.Join("\n", offenders));
    }

    /// <summary>
    /// <b>The list retires itself.</b> An entry for a client that has since been governed, or that
    /// no longer exists, would leave the rule above quietly accepting a name nothing registers —
    /// and the next ungoverned client to take that name would be exempt by accident.
    /// </summary>
    [Fact]
    public void Every_declared_exemption_still_describes_a_client_that_needs_one()
    {
        var registrations = Registrations().ToList();

        var stale = Ungoverned
            .Select(entry => (entry, match: registrations.SingleOrDefault(
                registration => string.Equals(
                    registration.Client, entry.Client, StringComparison.Ordinal))))
            .Where(pair => pair.match is null || pair.match.Governed)
            .Select(pair => pair.match is null
                ? $"{pair.entry.Client} is exempted and nothing registers it"
                : $"{pair.entry.Client} is exempted and now carries the handler; remove the entry")
            .ToList();

        stale.Should().BeEmpty(
            "the exemption list is the current record of what is not governed:\n" +
            string.Join("\n", stale));
    }

    /// <summary>
    /// <b>The payment dependency is never retried at the transport, and this is the invariant the
    /// handler is built on.</b> The executor resolves retry by dependency name and falls back to
    /// <c>Retry.Default</c> — three attempts — for a name that declares none. So the absence of a
    /// policy here does not mean "no retry"; it means three retries on a charge, against a provider
    /// that may already have taken the money, which is the failure the breaker's own documentation
    /// names.
    /// <para>
    /// Asserted against the composed catalogue rather than the file, because what matters is what
    /// the executor will resolve at run time after every contributor has been read.
    /// </para>
    /// </summary>
    [Fact]
    public void The_payment_dependency_declares_a_policy_that_never_retries()
    {
        var policies = ModuleAssemblyScanner
            .CreateAll<IOperationalPolicyContributor>(
                ModuleAssemblyScanner.FindPlatformAssemblies())
            .SelectMany(contributor => contributor.Policies)
            .ToList();

        policies.Should().NotBeEmpty("the platform declares operational policies");

        var retry = policies.OfType<RetryPolicy>().SingleOrDefault(policy => string.Equals(
            policy.Name, OperationalPolicyNames.CircuitBreaker.PaymentProvider,
            StringComparison.Ordinal));

        retry.Should().NotBeNull(
            "'{0}' must declare its own retry policy. Without one the executor falls back to " +
            "'{1}', and every payment call gains retries nobody asked for",
            OperationalPolicyNames.CircuitBreaker.PaymentProvider,
            OperationalPolicyNames.Retry.Default);

        retry!.MaxAttempts.Should().Be(
            0, "a charge is not idempotent, and the executor's retry re-sends one request message");

        policies.OfType<CircuitBreakerPolicy>().Should().Contain(
            policy => policy.Name == OperationalPolicyNames.CircuitBreaker.PaymentProvider,
            "the breaker this was all for is still declared");
    }

    /// <summary>
    /// What the scan read, so none of the rules above can pass by having found nothing. Every one
    /// of them is satisfied by an empty scan — a renamed method, a changed registration style, a
    /// filter that excluded everything.
    /// </summary>
    [Fact]
    public void The_scan_found_the_platforms_outbound_clients()
    {
        var registrations = Registrations().ToList();

        registrations.Should().HaveCountGreaterThanOrEqualTo(
            15, "eleven payment clients plus email, maps, integrations, trust and crime data");

        registrations.Count(registration => registration.Governed).Should().BeGreaterThanOrEqualTo(
            11, "the three payment adapters hold eleven clients between them");

        registrations.Select(registration => registration.Client).Should().OnlyHaveUniqueItems(
            "a client name registered twice is two clients sharing one configuration, and the " +
            "exemption list resolves names one-to-one");
    }

    // ── The scan ─────────────────────────────────────────────────────────

    private sealed record Registration(string Client, string Where, bool Governed);

    /// <summary>
    /// Every <c>AddHttpClient</c> in the platform's own source, and whether its chain carries the
    /// handler.
    /// <para>
    /// Read off the registration rather than off the composed service collection, because a
    /// reflective check would need a built host and would say nothing about where the offending
    /// line is. The scan is narrow enough to be obvious: a named client is registered by exactly
    /// one call shape in this repository.
    /// </para>
    /// </summary>
    private static IEnumerable<Registration> Registrations()
    {
        foreach (var file in SourceTree.ProductionFiles)
        {
            foreach (var invocation in file.Root.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(invocation => invocation.Expression.ToString()
                    .EndsWith("AddHttpClient", StringComparison.Ordinal)))
            {
                var client = invocation.ArgumentList.Arguments.FirstOrDefault()?.ToString();

                // The unnamed overload configures the default client and names no dependency.
                if (client is null || !client.EndsWith("HttpClientName", StringComparison.Ordinal))
                {
                    continue;
                }

                // The whole fluent chain, which is the statement the registration sits in: the
                // handler is attached by a later link, so the invocation alone never shows it.
                var statement = invocation.Ancestors()
                    .OfType<StatementSyntax>()
                    .FirstOrDefault();

                var governed = statement?.ToString()
                    .Contains(Governs, StringComparison.Ordinal) == true;

                yield return new Registration(client, file.Locate(invocation), governed);
            }
        }
    }
}
