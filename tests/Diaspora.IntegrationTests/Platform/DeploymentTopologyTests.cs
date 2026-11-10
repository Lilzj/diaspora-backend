using Diaspora.IntegrationTests.Common;
using Diaspora.Platform.Operations.Scalability;
using Diaspora.Platform.Validation;
using Diaspora.Platform.Validation.Validators;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diaspora.IntegrationTests.Platform;

/// <summary>
/// BARCH-002's verification criterion, asserted against the real composed platform.
/// <para>
/// <b>A unit test proves the validator reasons correctly about a stub. These prove it reasons
/// correctly about this platform.</b> The substantive claim in the finding is not "a lock that
/// returns false is refused" — it is that <em>the lock this platform actually registers</em> does
/// not span instances, and that the resulting report is one the host stops for. Both are read from
/// the real service provider and the real registration snapshot, so a future registration that
/// quietly replaced the lock would change this result.
/// </para>
/// <para>
/// <b>What is deliberately not asserted here, and why.</b> Booting a host with two instances
/// declared does refuse — it never serves a request — but the reason cannot be recovered from this
/// harness. When platform validation throws, the host is disposed and
/// <c>WebApplicationFactory</c> then resolves <c>IServer</c> from the disposed provider, so the
/// caller receives an <c>ObjectDisposedException</c> rather than
/// <c>PlatformValidationException</c>; <c>CreateHost</c> returns without throwing at all, and the
/// error lines the runner writes on the way down go to Serilog rather than to a provider a test can
/// add. A test asserting only "the host threw" would pass against a host that failed to start for
/// any unrelated reason, which is a green test that proves nothing. The chain is therefore closed
/// in pieces that can each be proved: the findings come from the real registrations below, the
/// report they produce is one <see cref="StartupValidationReport.ShouldStopTheHost"/> stops for,
/// and the runner's throwing on such a report is the behaviour every other test in this project
/// depends on to start at all.
/// </para>
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class DeploymentTopologyTests
{
    private readonly CustomWebApplicationFactory _factory;

    public DeploymentTopologyTests(CustomWebApplicationFactory factory) => _factory = factory;

    /// <summary>
    /// Two instances against the platform exactly as it ships. Both findings must appear: an
    /// operator who closes the first and redeploys should not discover the second on the next boot.
    /// </summary>
    [Fact]
    public void Declaring_two_instances_against_the_real_platform_produces_a_report_that_stops_the_host()
    {
        var findings = ValidateAs(new DeploymentOptions { InstanceCount = 2 });

        var codes = findings.Select(finding => finding.Code).ToList();

        codes.Should().Contain(
            "SV8002",
            "the IDistributedLock this platform registers coordinates within one process, so every "
            + "timer-driven sweep would run on every instance");

        codes.Should().Contain(
            "SV8003",
            "the rate limiter partitions in memory, so each instance enforces every budget again");

        findings.Should().OnlyContain(
            finding => finding.Severity == StartupFindingSeverity.Error,
            "a warning is something an operator fixes on their own schedule, and this is not that");

        new StartupValidationReport(1, findings)
            .ShouldStopTheHost(failOnWarning: false)
            .Should().BeTrue(
                "producing findings the host starts anyway would leave the constraint documented "
                + "rather than enforced, which is the state BARCH-002 describes");
    }

    /// <summary>
    /// The real lock, named rather than inferred. If someone registers a distributed lock, this is
    /// the test that should fail and send them to the validator — SV8002 would stop firing, and a
    /// report asserting only "two instances are refused" would keep passing on SV8003 alone and
    /// hide that half the constraint had been lifted.
    /// </summary>
    [Fact]
    public void The_lock_this_platform_registers_does_not_span_instances()
    {
        using var scope = _factory.Services.CreateScope();

        var locks = scope.ServiceProvider
            .GetRequiredService<Diaspora.Platform.Operations.Scheduling.IDistributedLock>();

        locks.IsDistributed.Should().BeFalse(
            "the shipped lock is in-process and says so; when that changes, DeploymentTopologyValidator "
            + "and the single-instance note in fly.toml both need revisiting");
    }

    /// <summary>
    /// The other direction, and the one that stops these becoming tests a validator that refused
    /// everything would also pass. Read from the real report rather than from the host having
    /// started, because "it booted" cannot tell a clean deployment apart from one whose validator
    /// never ran — so the validator is asserted to be present in the same breath.
    /// </summary>
    [Fact]
    public void The_shipped_single_instance_topology_is_clean()
    {
        using var scope = _factory.Services.CreateScope();

        scope.ServiceProvider.GetServices<Diaspora.Platform.Validation.IStartupValidator>()
            .Select(validator => validator.GetType())
            .Should().Contain(
                typeof(DeploymentTopologyValidator),
                "a clean report from a validator that was never discovered is not a clean platform");

        var report = _factory.Services
            .GetRequiredService<PlatformStartupValidationService>()
            .Run(new PlatformValidationOptions());

        report.Findings
            .Where(finding => finding.Code.StartsWith("SV80", StringComparison.Ordinal))
            .Should().BeEmpty(
                "one instance is the topology the platform supports, and a constraint that "
                + "refused the supported topology would be worse than no constraint");
    }

    /// <summary>
    /// Runs the validator against the host's own provider and registration snapshot, with only the
    /// declared instance count substituted. Everything else — the lock, the configuration, the
    /// descriptors — is what this platform composed.
    /// </summary>
    private List<StartupFinding> ValidateAs(DeploymentOptions deployment)
    {
        using var scope = _factory.Services.CreateScope();

        var snapshot = _factory.Services.GetRequiredService<ServiceRegistrationSnapshot>();

        var context = new StartupValidationContext(
            new SubstitutedOptions(scope.ServiceProvider, Options.Create(deployment)),
            snapshot.Descriptors,
            snapshot.Assemblies);

        return [.. new DeploymentTopologyValidator().Validate(context)];
    }

    /// <summary>
    /// The real provider with one answer replaced.
    /// <para>
    /// A substitution rather than a second host: building a host per instance count would cost a
    /// full composition each time, and the thing under test is what the validator concludes from
    /// this platform's registrations, not whether configuration binding works — which is asserted
    /// where it can be, in the unit suite.
    /// </para>
    /// </summary>
    private sealed class SubstitutedOptions : IServiceProvider
    {
        private readonly IServiceProvider _inner;
        private readonly IOptions<DeploymentOptions> _deployment;

        public SubstitutedOptions(IServiceProvider inner, IOptions<DeploymentOptions> deployment)
        {
            _inner = inner;
            _deployment = deployment;
        }

        public object? GetService(Type serviceType) =>
            serviceType == typeof(IOptions<DeploymentOptions>)
                ? _deployment
                : _inner.GetService(serviceType);
    }
}
