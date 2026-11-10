using System.Reflection;
using Diaspora.Platform.Modules;
using Diaspora.Platform.Operations.Scalability;
using Diaspora.Platform.Operations.Scheduling;
using Diaspora.Platform.Validation;
using Diaspora.Platform.Validation.Validators;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diaspora.UnitTests.Platform;

/// <summary>
/// The refusal that makes the single-instance constraint real.
/// <para>
/// <b>Every failure this guards is silent.</b> A deployment scaled past one instance does not
/// crash, log or fail a test: it enforces every rate limit once per machine, runs every sweep once
/// per machine, and delivers real-time messages to a fraction of the members who should get them.
/// The platform ships no way to detect any of that at runtime, so the only moment it can be caught
/// is before the host serves its first request — which makes these tests the only thing standing
/// behind the constraint besides prose.
/// </para>
/// </summary>
public class DeploymentTopologyValidatorTests
{
    private readonly DeploymentTopologyValidator _validator = new();

    [Fact]
    public void The_shipped_defaults_produce_nothing_to_report()
    {
        Validate(new DeploymentOptions())
            .Should().BeEmpty(
                "a fresh clone must boot clean, or the report becomes noise nobody reads");
    }

    /// <summary>
    /// Zero instances is not a smaller deployment, it is a configuration nobody meant. Reported on
    /// its own: the checks below reason about how many instances there are, and running them on a
    /// nonsensical count reports on a topology that does not exist.
    /// </summary>
    [Fact]
    public void An_instance_count_below_one_is_refused_and_stops_the_rest_of_the_report()
    {
        var findings = Validate(new DeploymentOptions { InstanceCount = 0 });

        findings.Should().ContainSingle()
            .Which.Code.Should().Be("SV8001");

        findings[0].Severity.Should().Be(StartupFindingSeverity.Error);
    }

    /// <summary>
    /// The finding BARCH-002 is about. The sweeps already take a lock — what they do not have is a
    /// lock that means anything on a second machine.
    /// </summary>
    [Fact]
    public void More_than_one_instance_without_an_instance_spanning_lock_is_refused()
    {
        var findings = Validate(
            new DeploymentOptions { InstanceCount = 3 },
            realtimeInstanceCount: "3",
            locks: new StubLock(isDistributed: false));

        var finding = findings.Should().ContainSingle(f => f.Code == "SV8002").Which;

        finding.Severity.Should().Be(StartupFindingSeverity.Error);
        finding.Message.Should().Contain("recurring donations",
            "the message has to name the sweep where running twice is a second charge, not just " +
            "say that sweeps double");
    }

    /// <summary>
    /// A registration that is missing entirely must fail the same way as one that does not span
    /// instances. Absent coordination and in-process coordination produce the same duplicated
    /// sweeps, so treating "no lock registered" as acceptable would be the fail-open case.
    /// </summary>
    [Fact]
    public void A_missing_lock_registration_counts_as_not_spanning_instances()
    {
        Validate(
            new DeploymentOptions { InstanceCount = 2 },
            realtimeInstanceCount: "2",
            locks: null)
            .Should().Contain(f => f.Code == "SV8002");
    }

    /// <summary>
    /// Half the constraint is not the constraint. Registering a real lock closes the sweep half and
    /// leaves the rate limiter partitioning in memory, so the host must still refuse — and must
    /// refuse for the remaining reason rather than the one that has been fixed.
    /// </summary>
    [Fact]
    public void An_instance_spanning_lock_closes_its_own_finding_and_not_the_others()
    {
        var findings = Validate(
            new DeploymentOptions { InstanceCount = 2 },
            realtimeInstanceCount: "2",
            locks: new StubLock(isDistributed: true));

        findings.Should().NotContain(f => f.Code == "SV8002");

        var finding = findings.Should().ContainSingle(f => f.Code == "SV8003").Which;

        finding.Severity.Should().Be(StartupFindingSeverity.Error);
        finding.Message.Should().Contain("2 times the configured one");
    }

    /// <summary>
    /// The scenario the duplicate exists to catch: the platform's count is raised and the module's
    /// is left alone, so the real-time runtime still believes it is the only instance and never
    /// asks for the backplane it now needs.
    /// </summary>
    [Fact]
    public void A_realtime_instance_count_that_disagrees_is_refused()
    {
        var finding = Validate(
            new DeploymentOptions { InstanceCount = 3 },
            realtimeInstanceCount: "1",
            locks: new StubLock(isDistributed: true))
            .Should().ContainSingle(f => f.Code == "SV8004").Which;

        finding.Severity.Should().Be(StartupFindingSeverity.Error);
        finding.Message.Should().Contain("3").And.Contain("1");
    }

    /// <summary>
    /// An absent key is not an absent opinion: it is the Notifications default, which is one. A
    /// deployment that raised the platform's count and never wrote the module's is exactly the
    /// half-configured case, so it must read as a disagreement rather than as silence.
    /// </summary>
    [Fact]
    public void An_absent_realtime_instance_count_is_read_as_one()
    {
        Validate(
            new DeploymentOptions { InstanceCount = 2 },
            realtimeInstanceCount: null,
            locks: new StubLock(isDistributed: true))
            .Should().Contain(f => f.Code == "SV8004");

        Validate(
            new DeploymentOptions { InstanceCount = 1 },
            realtimeInstanceCount: null)
            .Should().NotContain(f => f.Code == "SV8004");
    }

    /// <summary>
    /// A value that cannot be parsed would bind to the default and agree with a single-instance
    /// platform by accident, which is the quietest possible way for a typo to disable a check.
    /// </summary>
    [Fact]
    public void A_realtime_instance_count_that_is_not_a_number_is_refused()
    {
        Validate(new DeploymentOptions(), realtimeInstanceCount: "two")
            .Should().ContainSingle(f => f.Code == "SV8004")
            .Which.Message.Should().Contain("not a whole number");
    }

    /// <summary>
    /// With no configuration at all — a unit-test host, or one built by hand — the contradiction
    /// check has nothing to compare and must stay quiet rather than invent a disagreement.
    /// </summary>
    [Fact]
    public void A_host_with_no_configuration_reports_no_contradiction()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IOptions<DeploymentOptions>>(
            Options.Create(new DeploymentOptions()));

        var registrations = services.ToList();
        using var provider = services.BuildServiceProvider();

        _validator
            .Validate(new StartupValidationContext(
                provider, registrations, [Assembly.GetExecutingAssembly()]))
            .Should().BeEmpty();
    }

    /// <summary>
    /// The step between a correct validator and an enforced constraint: the key an operator writes
    /// has to reach the options the validator reads.
    /// <para>
    /// Asserted through <c>AddPlatformKernel</c> rather than by binding a section here, because the
    /// failure this guards is the registration binding to a different section than the deployment
    /// writes — a mistake that leaves every other test on this page green, the host booting
    /// happily, and the declaration in <c>fly.toml</c> inert. The platform has had this exact bug
    /// before, where a flat environment variable bound to nothing and the override reported success
    /// while changing nothing.
    /// </para>
    /// </summary>
    [Fact]
    public void The_deployment_section_an_operator_writes_is_the_one_the_validator_reads()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Deployment:InstanceCount"] = "4"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddPlatformKernel(configuration, []);

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<DeploymentOptions>>()
            .Value.InstanceCount.Should().Be(
                4,
                "Deployment:InstanceCount is what fly.toml sets through Deployment__InstanceCount, "
                + "and a validator reading a section nobody writes enforces nothing");
    }

    private List<StartupFinding> Validate(
        DeploymentOptions deployment,
        string? realtimeInstanceCount = null,
        IDistributedLock? locks = null)
    {
        var settings = new Dictionary<string, string?>(StringComparer.Ordinal);

        if (realtimeInstanceCount is not null)
        {
            settings["Notifications:Realtime:InstanceCount"] = realtimeInstanceCount;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IOptions<DeploymentOptions>>(Options.Create(deployment));

        if (locks is not null)
        {
            services.AddSingleton(locks);
        }

        var registrations = services.ToList();
        var provider = services.BuildServiceProvider();

        return [.. _validator.Validate(new StartupValidationContext(
            provider, registrations, [Assembly.GetExecutingAssembly()]))];
    }

    /// <summary>
    /// A lock that reports whichever answer the test needs. The property is the whole point of the
    /// seam: an implementation cannot omit it, so a future distributed lock cannot be assumed
    /// in-process and an in-process one cannot be assumed distributed.
    /// </summary>
    private sealed class StubLock : IDistributedLock
    {
        public StubLock(bool isDistributed) => IsDistributed = isDistributed;

        public bool IsDistributed { get; }

        public Task<ILeaseHandle?> TryAcquireAsync(
            string resource,
            TimeSpan timeToLive,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ILeaseHandle?>(null);

        public Task<string?> FindHolderAsync(
            string resource,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }
}
