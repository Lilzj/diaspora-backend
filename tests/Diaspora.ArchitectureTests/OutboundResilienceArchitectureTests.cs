using Diaspora.Platform.Modules;
using Diaspora.Platform.Operations.Policies;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// Every call that leaves the process is governed by a policy somebody can read, and every policy
/// written down governs something.
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
/// What it did not have was a caller: the payment breaker's name appeared in exactly two places in
/// the repository — where the constant is declared, and where the policy is declared.
/// </para>
/// <para>
/// <b>And the first fix left the same defect standing in two places.</b> Firebase Cloud Messaging
/// was ungoverned and <em>invisible</em>: the scan below used to recognise a client registration by
/// the <em>name of the constant</em> naming it, so it saw every client called
/// <c>HttpClientName</c> and neither of Firebase's two. A scan that recognises its population by
/// naming convention finds whatever already follows the convention, and a floor on the count —
/// "at least fifteen" — is satisfied by seventeen with two missing. So the vacuity guard here
/// counts nothing: it asserts that every project which registers a client contributed one.
/// </para>
/// </summary>
public sealed class OutboundResilienceArchitectureTests
{
    private const string Governs = "WithPlatformResilience";

    /// <summary>
    /// A client that is not routed through <see cref="Governs"/>, and why that is allowed.
    /// </summary>
    /// <param name="Client">The dependency the client is known by, resolved — <c>email.resend</c>.</param>
    /// <param name="Why">
    /// <b>This column used to hold two different things and keeping them apart was the point.</b> A
    /// client governed at its call site is finished work: the executor runs it, the breaker sees
    /// it, and a handler would be a second wrapper around the same thing. A client governed by
    /// nothing was not finished, and writing that down is how it stopped being invisible. There
    /// are none of the second kind left; an entry claiming to be one should be read as work in
    /// progress rather than as a decision.
    /// </param>
    private sealed record UngovernedClient(string Client, string Why);

    /// <summary>
    /// The clients that do not carry the handler, measured 23 November 2026.
    /// <para>
    /// Every one of them is governed where it is called instead. The four that were governed by
    /// nothing — both Google Maps clients, the Police UK crime data read and the Trust decision
    /// publisher — now carry the handler, as do Firebase's two, which this list never knew about.
    /// </para>
    /// </summary>
    private static readonly UngovernedClient[] Ungoverned =
    [
        new("email.resend",
            "Governed at the call site: GatewayEmailSender runs every send through " +
            "IResilienceExecutor under email.resend, with its own retry, breaker and classifier."),

        new("diaspora.integrations",
            "Governed at the call site: IntegrationHttpGateway is the executor seam for every " +
            "outbound integration call, which is what B17-R4 exists to keep it."),

        new("sms.twilio",
            "Governed at the call site: RoutedSmsOtpDeliveryService resolves the dependency per " +
            "provider — sms.twilio — and runs the dispatch through IResilienceExecutor. The " +
            "per-provider name is the point: one gateway's failures must not break the other, " +
            "because the router's whole job is to fall through to it."),

        new("sms.africas-talking",
            "Governed at the call site under sms.africas-talking. Same as the Twilio client.")
    ];

    /// <summary>
    /// A declared circuit breaker that nothing names as a dependency, and why it is declared anyway.
    /// </summary>
    /// <param name="Policy">
    /// The constant on <c>OperationalPolicyNames.CircuitBreaker</c>, by its member name.
    /// </param>
    /// <param name="Why">
    /// What the breaker is waiting for. <b>A reserved policy is a prediction, and a prediction
    /// needs a date and a dependency that does not exist yet</b> — otherwise it is the defect this
    /// row is about, written down and thereby excused.
    /// </param>
    private sealed record ReservedBreaker(string Policy, string Why);

    /// <summary>
    /// The breakers with no dependency, and the reason each has none, measured 23 November 2026.
    /// </summary>
    private static readonly ReservedBreaker[] Reserved =
    [
        new(nameof(OperationalPolicyNames.CircuitBreaker.MediaProvider),
            "FilesystemStorageProvider is the only IMediaStorageProvider in the repository, so " +
            "no media call leaves the process. Reserved for the first object-storage adapter."),

        new(nameof(OperationalPolicyNames.CircuitBreaker.SearchProvider),
            "Every search port is backed by the database engine the platform ships — the policy's " +
            "own description says so — so there is no search provider to break. Reserved for the " +
            "first external one."),

        new(nameof(OperationalPolicyNames.CircuitBreaker.AiProvider),
            "B16's, declared in B17 on the reasoning that the breaker protecting the platform " +
            "from an AI provider is an operational concern rather than the first caller's."),

        new(nameof(OperationalPolicyNames.CircuitBreaker.DistributedCache),
            "PlatformCacheService resolves a CachePolicy directly and never executes through " +
            "IResilienceExecutor, so cache.distributed is a name the executor is never asked " +
            "about. Reserved for a cache adapter that calls out of the process.")
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
            .Where(registration => registration.Dependency is null)
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
            .Where(pair => pair.match is null || pair.match.Dependency is not null)
            .Select(pair => pair.match is null
                ? $"{pair.entry.Client} is exempted and nothing registers it"
                : $"{pair.entry.Client} is exempted and now carries the handler; remove the entry")
            .ToList();

        stale.Should().BeEmpty(
            "the exemption list is the current record of what is not governed:\n" +
            string.Join("\n", stale));
    }

    /// <summary>
    /// <b>No governed client is ever retried at the transport, and this is the invariant the
    /// handler is built on.</b> The executor resolves retry by dependency name and falls back to
    /// <c>Retry.Default</c> — three attempts — for a name that declares none. So the absence of a
    /// policy does not mean "no retry"; it means three retries on a request the handler cannot
    /// rebuild, and on a payment path, three attempts at a charge against a provider that may
    /// already have taken the money.
    /// <para>
    /// Asserted against the composed catalogue rather than the file, because what matters is what
    /// the executor will resolve at run time after every contributor has been read. That is also
    /// what ties the four dependency names written as literals in the contributor to the constants
    /// the adapters pass: a typo on either side leaves a governed client with no policy here.
    /// </para>
    /// </summary>
    [Fact]
    public void Every_governed_client_declares_a_policy_that_never_retries()
    {
        var policies = ComposedPolicies();
        var governed = Registrations().Where(entry => entry.Dependency is not null).ToList();

        governed.Should().NotBeEmpty("the platform governs outbound clients");

        var offenders = governed
            .Select(entry => (entry, retry: policies.OfType<RetryPolicy>().SingleOrDefault(
                policy => string.Equals(
                    policy.Name, entry.Dependency, StringComparison.Ordinal))))
            .Where(pair => pair.retry is null || pair.retry.MaxAttempts > 0)
            .Select(pair => pair.retry is null
                ? $"{pair.entry.Where}: '{pair.entry.Dependency}' is governed and declares no " +
                  $"retry policy, so the executor falls back to '{OperationalPolicyNames.Retry.Default}'"
                : $"{pair.entry.Where}: '{pair.entry.Dependency}' permits " +
                  $"{pair.retry.MaxAttempts} retries of a request the handler cannot rebuild")
            .Order(StringComparer.Ordinal)
            .ToList();

        offenders.Should().BeEmpty(
            "ResilientHttpHandler refuses to be constructed for any of these, which arrives at " +
            "the first call through that client; this rule is what makes it arrive at build " +
            "time:\n" + string.Join("\n", offenders));
    }

    /// <summary>
    /// <b>One breaker per payment provider, never one for the class of them.</b> The executor keys
    /// breaker state by dependency name, so a name shared across the three adapters would mean one
    /// provider's outage opening the circuit on the other two — and
    /// <c>IPaymentGatewayRegistry.Resolve</c> picks a gateway by currency and country, so a
    /// degraded corridor would refuse a payment that was never going to touch it.
    /// <para>
    /// The email adapter states the same rule in its own words, and has since it was written:
    /// <em>"an unrecognised provider gets its own dependency so its failures stay off every other
    /// provider's breaker"</em>. This is that sentence, enforced where three providers are
    /// genuinely alternatives.
    /// </para>
    /// </summary>
    [Fact]
    public void No_two_payment_adapters_share_a_breaker()
    {
        var byAdapter = Registrations()
            .Where(entry => entry.Project.StartsWith("Diaspora.Payments.", StringComparison.Ordinal))
            .GroupBy(entry => entry.Project, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(entry => entry.Dependency).Distinct(StringComparer.Ordinal)
                    .ToList(),
                StringComparer.Ordinal);

        byAdapter.Should().HaveCount(
            3, "Stripe, PayPal and Flutterwave each ship an adapter assembly");

        var breakers = ComposedPolicies().OfType<CircuitBreakerPolicy>().ToList();

        var faults = new List<string>();

        foreach (var (adapter, dependencies) in byAdapter.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (dependencies.Count != 1 || dependencies[0] is null)
            {
                faults.Add(
                    $"{adapter} spreads its clients over [{string.Join(", ", dependencies)}]; a " +
                    "payment adapter is one provider and takes one dependency name");
                continue;
            }

            if (!breakers.Any(policy => string.Equals(
                    policy.Name, dependencies[0], StringComparison.Ordinal)))
            {
                faults.Add(
                    $"{adapter} is governed under '{dependencies[0]}', which declares no circuit " +
                    $"breaker, so it silently inherits '{OperationalPolicyNames.CircuitBreaker.ExternalApi}' " +
                    "— a laxer breaker than the one a payment gateway is supposed to have");
            }
        }

        var shared = byAdapter
            .SelectMany(pair => pair.Value.Select(dependency => (pair.Key, dependency)))
            .GroupBy(pair => pair.dependency, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group =>
                $"'{group.Key}' is the dependency of {string.Join(" and ", group.Select(pair => pair.Key))}; " +
                "the slowest of them would open the circuit on the rest");

        faults.AddRange(shared);

        faults.Should().BeEmpty(
            "a shared breaker turns one provider's outage into an outage of the whole payment " +
            "surface:\n" + string.Join("\n", faults));
    }

    /// <summary>
    /// <b>A declared breaker either guards something or says what it is waiting for.</b> This is
    /// the rule the first half of BARCH-007 needed and did not have. The payment breaker was
    /// declared with the strictest settings on the platform, documented, put on the operator
    /// dashboard, given isolate and reset controls and a health contributor — and never named by
    /// anything, for two phases, with no test anywhere able to say so.
    /// <para>
    /// "Named by something" rather than "executed at run time", because the dependency a call site
    /// passes is computed: <c>EmailDependencies.For(provider)</c> returns
    /// <c>email.{provider}</c> for a provider nobody anticipated, which is deliberate and
    /// unresolvable from syntax. What is checkable is that somebody outside the two declaration
    /// files refers to the name at all, which is exactly the measurement that was missing.
    /// </para>
    /// </summary>
    [Fact]
    public void Every_declared_circuit_breaker_is_named_by_something_or_reserved()
    {
        var reserved = Reserved.ToDictionary(
            entry => entry.Policy, entry => entry.Why, StringComparer.Ordinal);

        var declared = typeof(OperationalPolicyNames.CircuitBreaker)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => field.Name)
            .ToList();

        declared.Should().HaveCountGreaterThanOrEqualTo(
            5, "the platform declares a breaker per provider plus the external-API fallback");

        var faults = new List<string>();

        foreach (var member in declared.Order(StringComparer.Ordinal))
        {
            var named = BreakerReferences(member) > 0;
            var isReserved = reserved.ContainsKey(member);

            if (!named && !isReserved)
            {
                faults.Add(
                    $"CircuitBreaker.{member} is declared and nothing outside OperationalPolicyNames " +
                    "and the policy contributor names it. Route a client or a call site through it, " +
                    "or add it to Reserved with what it is waiting for.");
            }
            else if (named && isReserved)
            {
                faults.Add(
                    $"CircuitBreaker.{member} is reserved and is now named by something; remove " +
                    "the Reserved entry so the rule above starts guarding it.");
            }
        }

        faults.Should().BeEmpty(
            "a breaker with no caller is the defect this row is about:\n" + string.Join("\n", faults));
    }

    /// <summary>
    /// <b>Every reserved entry says what it is waiting for.</b> A reason is the whole value of the
    /// list — "not used yet" would make the rule above a switch somebody flips to make a build
    /// green.
    /// </summary>
    [Fact]
    public void Every_reserved_breaker_explains_itself()
    {
        var declared = typeof(OperationalPolicyNames.CircuitBreaker)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => field.Name)
            .ToHashSet(StringComparer.Ordinal);

        var faults = Reserved
            .Select(entry => entry switch
            {
                _ when !declared.Contains(entry.Policy) =>
                    $"{entry.Policy} is reserved and OperationalPolicyNames.CircuitBreaker no " +
                    "longer declares it",
                { Why.Length: < 60 } =>
                    $"{entry.Policy}'s reason is too short to be one",
                _ => null
            })
            .Where(fault => fault is not null)
            .ToList();

        faults.Should().BeEmpty(string.Join("\n", faults));
    }

    /// <summary>
    /// <b>What the scan read — measured against where clients live rather than against a number.</b>
    /// <para>
    /// The guard this replaces asserted "at least fifteen registrations", and seventeen satisfied
    /// it while two were missing entirely. A floor cannot see a hole in its own population. This
    /// one derives the expected population from the same source as the result: every project with
    /// an <c>AddHttpClient</c> call must contribute a client to the scan, so a project the filter
    /// excludes fails by name.
    /// </para>
    /// </summary>
    [Fact]
    public void Every_project_that_registers_a_client_contributed_one()
    {
        var registering = SourceTree.ProductionFiles
            .Where(file => file.Root.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Any(invocation => invocation.Expression.ToString()
                    .EndsWith("AddHttpClient", StringComparison.Ordinal)))
            .Select(ProjectOf)
            .ToHashSet(StringComparer.Ordinal);

        var scanned = Registrations().Select(entry => entry.Project).ToHashSet(StringComparer.Ordinal);

        registering.Should().NotBeEmpty("the platform registers HTTP clients");

        var missing = registering.Except(scanned, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(project =>
                $"{project} calls AddHttpClient and contributed no client to the scan, so every " +
                "rule in this file is silent about it")
            .ToList();

        missing.Should().BeEmpty(string.Join("\n", missing));

        Registrations().Select(entry => entry.Client).Should().OnlyHaveUniqueItems(
            "a client name registered twice is two clients sharing one configuration, and the " +
            "exemption list resolves names one-to-one");
    }

    // ── The scan ─────────────────────────────────────────────────────────

    private sealed record Registration(string Client, string Project, string Where, string? Dependency);

    /// <summary>
    /// Every named <c>AddHttpClient</c> in the platform's own source, the dependency it is governed
    /// under if any, and both resolved from the constants they are written with.
    /// <para>
    /// <b>A registration is recognised by its shape, not by the name of the constant naming it.</b>
    /// The first argument of <c>AddHttpClient</c> is either the client's name or a configuration
    /// delegate; anything that is not a lambda is a name. That is the whole filter, and it is what
    /// the previous one — "the argument's text ends in HttpClientName" — should have been.
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
                var first = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;

                // The unnamed overload configures the default client and names no dependency.
                if (first is null or SimpleLambdaExpressionSyntax or ParenthesizedLambdaExpressionSyntax)
                {
                    continue;
                }

                // The whole fluent chain, which is the statement the registration sits in: the
                // handler is attached by a later link, so the invocation alone never shows it.
                var statement = invocation.Ancestors().OfType<StatementSyntax>().FirstOrDefault();

                var governs = statement?.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .FirstOrDefault(call => call.Expression.ToString()
                        .EndsWith(Governs, StringComparison.Ordinal));

                var dependency = governs?.ArgumentList.Arguments.FirstOrDefault()?.Expression;

                yield return new Registration(
                    Resolve(file, first) ?? first.ToString(),
                    ProjectOf(file),
                    file.Locate(invocation),
                    dependency is null ? null : Resolve(file, dependency) ?? dependency.ToString());
            }
        }
    }

    /// <summary>The <c>src/</c> directory a file belongs to — the assembly, by its folder name.</summary>
    private static string ProjectOf(SourceFile file)
    {
        var segments = file.Path.Split('/');

        return segments.Length > 1 ? segments[1] : file.Path;
    }

    /// <summary>
    /// The value of a <c>const string</c> an expression names, or <c>null</c> when it is computed,
    /// declared outside <c>src/</c> or ambiguous.
    /// <para>
    /// Resolving rather than comparing text is what lets one list hold Firebase's
    /// <c>TokenClientName</c>, Stripe's <c>HttpClientName</c> and a dependency written as
    /// <c>OperationalPolicyNames.CircuitBreaker.StripePayments</c> without three spellings of the
    /// same thing. An unresolvable name falls back to its own text, which fails a rule loudly
    /// rather than passing one quietly.
    /// </para>
    /// </summary>
    private static string? Resolve(SourceFile file, ExpressionSyntax expression) => expression switch
    {
        LiteralExpressionSyntax { Token.Value: string literal } => literal,

        // An unqualified name: a constant on the type the call sits in, so the file decides.
        IdentifierNameSyntax identifier =>
            Lookup($"{file.Path}|{identifier.Identifier.ValueText}"),

        // Type.Member, however many namespaces or outer types precede it. The segment immediately
        // left of the member is the declaring type, which for a nested class is the nested name:
        // OperationalPolicyNames.CircuitBreaker.StripePayments resolves under CircuitBreaker.
        MemberAccessExpressionSyntax member =>
            Lookup($"{Owner(member.Expression)}.{member.Name.Identifier.ValueText}"),

        _ => null
    };

    private static string Owner(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        _ => expression.ToString()
    };

    private static string? Lookup(string key) =>
        Constants.Value.TryGetValue(key, out var value) ? value : null;

    private static readonly Lazy<IReadOnlyDictionary<string, string?>> Constants = new(ReadConstants);

    /// <summary>
    /// Every <c>const string</c> in the platform's source, by <c>Type.Member</c> and by
    /// <c>file|Member</c>. A key two declarations disagree about is stored as <c>null</c>, so an
    /// ambiguous name resolves to nothing rather than to whichever file was read first.
    /// </summary>
    private static Dictionary<string, string?> ReadConstants()
    {
        var map = new Dictionary<string, string?>(StringComparer.Ordinal);

        void Record(string key, string value)
        {
            if (map.TryGetValue(key, out var existing)
                && !string.Equals(existing, value, StringComparison.Ordinal))
            {
                map[key] = null;
                return;
            }

            map[key] = value;
        }

        foreach (var file in SourceTree.ProductionFiles)
        {
            foreach (var field in file.Root.DescendantNodes().OfType<FieldDeclarationSyntax>())
            {
                if (!field.Modifiers.Any(token => token.ValueText == "const")
                    || field.Declaration.Type.ToString() != "string")
                {
                    continue;
                }

                var owner = field.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();

                foreach (var variable in field.Declaration.Variables)
                {
                    if (variable.Initializer?.Value is not LiteralExpressionSyntax
                        { Token.Value: string value })
                    {
                        continue;
                    }

                    if (owner is not null)
                    {
                        Record($"{owner.Identifier.ValueText}.{variable.Identifier.ValueText}", value);
                    }

                    Record($"{file.Path}|{variable.Identifier.ValueText}", value);
                }
            }
        }

        return map;
    }

    /// <summary>
    /// How many times something outside the two declaration files names
    /// <c>CircuitBreaker.<paramref name="member"/></c>.
    /// </summary>
    private static int BreakerReferences(string member) => SourceTree.ProductionFiles
        .Where(file => !file.Path.EndsWith("OperationalPolicyNames.cs", StringComparison.Ordinal))
        .Where(file => !file.Path.EndsWith(
            "PlatformResiliencePolicyContributor.cs", StringComparison.Ordinal))
        .SelectMany(file => file.Root.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        .Count(access => string.Equals(
                access.Name.Identifier.ValueText, member, StringComparison.Ordinal)
            && access.Expression.ToString().EndsWith("CircuitBreaker", StringComparison.Ordinal));

    /// <summary>
    /// The policy catalogue as the executor will see it at run time, after every contributor the
    /// platform ships has been read.
    /// </summary>
    private static List<OperationalPolicy> ComposedPolicies()
    {
        var policies = ModuleAssemblyScanner
            .CreateAll<IOperationalPolicyContributor>(ModuleAssemblyScanner.FindPlatformAssemblies())
            .SelectMany(contributor => contributor.Policies)
            .ToList();

        policies.Should().NotBeEmpty("the platform declares operational policies");

        return policies;
    }
}
