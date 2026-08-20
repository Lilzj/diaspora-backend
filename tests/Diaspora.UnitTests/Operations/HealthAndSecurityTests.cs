using Diaspora.Operations.Health;
using Diaspora.Operations.Security;
using Diaspora.Platform.Operations.Health;
using Diaspora.Platform.Operations.Policies;
using Diaspora.Platform.Operations.Security;
using Diaspora.UnitTests.Common;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diaspora.UnitTests.Operations;

/// <summary>
/// The health aggregator's mapping from module results to a platform verdict.
/// <para>
/// The mapping is the whole design. Without it, the first third-party provider to have a bad afternoon
/// takes every instance on the platform out of rotation, which turns a degraded feature into an outage
/// and makes the outage worse by concentrating load on the replicas that have the same problem.
/// </para>
/// </summary>
public sealed class PlatformHealthAggregatorTests
{
    [Fact]
    public async Task All_healthy_aggregates_to_healthy()
    {
        var aggregator = Build(Contributor("Alpha", Healthy("a")));

        (await aggregator.CheckAsync()).State.Should().Be(PlatformHealthState.Healthy);
    }

    [Fact]
    public async Task A_non_critical_unhealthy_check_degrades_rather_than_failing_the_instance()
    {
        var aggregator = Build(Contributor("Alpha",
            ModuleHealthResult.Unhealthy("provider", "Alpha", HealthDimension.ExternalProvider, "down")));

        (await aggregator.CheckAsync()).State.Should().Be(PlatformHealthState.Degraded,
            "a dependency the platform can operate without must not empty the load balancer");
    }

    [Fact]
    public async Task A_critical_unhealthy_check_makes_the_instance_unhealthy()
    {
        var aggregator = Build(Contributor("Alpha",
            ModuleHealthResult.Unhealthy("db", "Alpha", HealthDimension.Storage, "unreachable")));

        (await aggregator.CheckAsync()).State.Should().Be(PlatformHealthState.Unhealthy,
            "an instance that cannot reach the database cannot serve a single request");
    }

    [Fact]
    public async Task A_contributor_that_throws_becomes_an_unhealthy_result_rather_than_an_exception()
    {
        var aggregator = Build(new ThrowingContributor());

        var model = await aggregator.CheckAsync();

        model.Results.Should().ContainSingle();
        model.Results[0].State.Should().Be(PlatformHealthState.Unhealthy);
        model.Results[0].Error.Should().Contain("deliberate",
            "a health system that can be taken down by a health check is worse than no health system");
    }

    [Fact]
    public async Task Results_are_filtered_by_dimension_when_one_is_asked_for()
    {
        var aggregator = Build(Contributor(
            "Alpha",
            Healthy("cache", HealthDimension.Cache),
            Healthy("db", HealthDimension.Storage)));

        var model = await aggregator.CheckAsync(HealthDimension.Cache);

        model.Results.Should().ContainSingle(result => result.Name == "cache");
    }

    [Fact]
    public async Task Every_dimension_reports_the_worst_of_its_checks()
    {
        var aggregator = Build(Contributor(
            "Alpha",
            Healthy("one", HealthDimension.Cache),
            ModuleHealthResult.Degraded("two", "Alpha", HealthDimension.Cache, "slow")));

        var model = await aggregator.CheckAsync();

        model.ByDimension[HealthDimension.Cache].Should().Be(PlatformHealthState.Degraded);
    }

    [Fact]
    public async Task A_result_is_stamped_with_how_long_its_contributor_took()
    {
        var aggregator = Build(Contributor("Alpha", Healthy("a")));

        var model = await aggregator.CheckAsync();

        model.Results[0].Duration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero,
            "a contributor that forgot to time itself would report zero, and a zero duration on a " +
            "check that is timing out is the most misleading number the endpoint could show");
    }

    private static ModuleHealthResult Healthy(
        string name, HealthDimension dimension = HealthDimension.Dependency) =>
        ModuleHealthResult.Healthy(name, "Alpha", dimension, "fine");

    private static StubContributor Contributor(string module, params ModuleHealthResult[] results) =>
        new(module, results);

    private static PlatformHealthAggregator Build(params IModuleHealthContributor[] contributors)
    {
        var catalog = new OperationalPolicyCatalog(
        [
            new PolicyContributor(
                new HealthPolicy(OperationalPolicyNames.Health.Default) { CacheDuration = TimeSpan.Zero },
                new HealthPolicy(OperationalPolicyNames.Health.Storage) { Critical = true },
                new HealthPolicy(OperationalPolicyNames.Health.ExternalProvider) { Critical = false },
                new HealthPolicy(OperationalPolicyNames.Health.Cache) { Critical = false })
        ]);

        // A real container rather than a hand-rolled scope factory. The aggregator resolves its
        // contributors per pass precisely so a scoped one works, and a fake that handed back the same
        // instances every time would not exercise the thing that made this change necessary.
        var services = new ServiceCollection();

        foreach (var contributor in contributors)
        {
            services.AddScoped<IModuleHealthContributor>(_ => contributor);
        }

        return new PlatformHealthAggregator(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            catalog,
            new FakeClock(),
            NullLogger<PlatformHealthAggregator>.Instance);
    }

    private sealed class StubContributor : IModuleHealthContributor
    {
        private readonly ModuleHealthResult[] _results;

        public StubContributor(string module, ModuleHealthResult[] results)
        {
            Module = module;
            _results = results;
        }

        public string Module { get; }

        public Task<IReadOnlyList<ModuleHealthResult>> CheckAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ModuleHealthResult>>(_results);
    }

    private sealed class ThrowingContributor : IModuleHealthContributor
    {
        public string Module => "Broken";

        public Task<IReadOnlyList<ModuleHealthResult>> CheckAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("deliberate");
    }

    private sealed class PolicyContributor : IOperationalPolicyContributor
    {
        private readonly OperationalPolicy[] _policies;

        public PolicyContributor(params OperationalPolicy[] policies) => _policies = policies;

        public string Module => "Test";

        public IEnumerable<OperationalPolicy> Policies => _policies;
    }
}

/// <summary>
/// The secret store's validation, rotation and retirement.
/// <para>
/// The retirement window is the property worth testing hardest: rotating a signing key without one
/// invalidates every token issued under the old key at the instant of rotation, which signs every
/// member out at once.
/// </para>
/// </summary>
public sealed class ConfigurationSecretStoreTests
{
    [Fact]
    public async Task A_configured_secret_is_readable()
    {
        var store = Build(
            new Dictionary<string, string?> { ["Secrets:test.key"] = "a-value" },
            Requirement("test.key"));

        var value = await store.GetAsync("test.key");

        value.Should().NotBeNull();
        value!.Value.Should().Be("a-value");
        value.Version.Should().Be(1);
    }

    [Fact]
    public async Task A_missing_required_secret_is_a_validation_problem()
    {
        var store = Build([], Requirement("test.key"));

        (await store.ValidateAsync()).Should().ContainSingle(problem => problem.Contains("required"));
    }

    [Fact]
    public async Task A_missing_optional_secret_is_not_a_problem()
    {
        var store = Build([], Requirement("test.key") with { Required = false });

        (await store.ValidateAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task A_secret_below_its_minimum_length_is_reported_without_quoting_it()
    {
        var store = Build(
            new Dictionary<string, string?> { ["Secrets:test.key"] = "short" },
            Requirement("test.key") with { MinimumLength = 32 });

        var problems = await store.ValidateAsync();

        problems.Should().ContainSingle();
        problems[0].Should().NotContain("short",
            "a validation message that quoted the secret would put it in the startup log of every " +
            "instance");
    }

    [Fact]
    public async Task Rotation_installs_a_new_version_and_keeps_the_old_one_accepted()
    {
        var store = Build(
            new Dictionary<string, string?> { ["Secrets:test.key"] = "old-value" },
            Requirement("test.key"));

        await store.RotateAsync("test.key", "new-value", TimeSpan.FromMinutes(30));

        (await store.GetAsync("test.key"))!.Value.Should().Be("new-value");

        var accepted = await store.GetAcceptedAsync("test.key");

        accepted.Select(secret => secret.Value).Should().Contain("old-value",
            "a token signed under the previous key must keep verifying until it expires, or a " +
            "rotation signs every member out at once");
    }

    [Fact]
    public async Task The_retiring_version_stops_being_accepted_when_its_window_closes()
    {
        var clock = new FakeClock();

        var store = Build(
            new Dictionary<string, string?> { ["Secrets:test.key"] = "old-value" },
            clock,
            Requirement("test.key"));

        await store.RotateAsync("test.key", "new-value", TimeSpan.FromMinutes(30));

        clock.Advance(TimeSpan.FromMinutes(31));

        (await store.GetAcceptedAsync("test.key")).Select(secret => secret.Value)
            .Should().NotContain("old-value");
    }

    [Fact]
    public async Task A_rotation_below_the_minimum_length_is_refused()
    {
        var store = Build(
            new Dictionary<string, string?> { ["Secrets:test.key"] = "a-long-enough-original-value" },
            Requirement("test.key") with { MinimumLength = 16 });

        var rotate = async () => await store.RotateAsync("test.key", "short", TimeSpan.FromMinutes(30));

        await rotate.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task A_descriptor_carries_a_fingerprint_and_never_the_value()
    {
        var store = Build(
            new Dictionary<string, string?> { ["Secrets:test.key"] = "the-secret-value" },
            Requirement("test.key"));

        var descriptor = await store.DescribeAsync("test.key");

        descriptor.Should().NotBeNull();
        descriptor!.Fingerprint.Should().HaveLength(12);
        descriptor.Fingerprint.Should().NotContain("secret");
    }

    [Fact]
    public async Task Two_different_values_fingerprint_differently()
    {
        var first = await Build(
            new Dictionary<string, string?> { ["Secrets:test.key"] = "one" },
            Requirement("test.key")).DescribeAsync("test.key");

        var second = await Build(
            new Dictionary<string, string?> { ["Secrets:test.key"] = "two" },
            Requirement("test.key")).DescribeAsync("test.key");

        first!.Fingerprint.Should().NotBe(second!.Fingerprint,
            "the fingerprint exists so two operators can confirm that staging and production hold " +
            "different keys without either revealing one");
    }

    [Fact]
    public async Task A_secret_past_its_rotation_schedule_is_reported_as_expiring()
    {
        var clock = new FakeClock();

        var store = Build(
            new Dictionary<string, string?> { ["Secrets:test.key"] = "a-value" },
            clock,
            Requirement("test.key") with { RotateEvery = TimeSpan.FromDays(30) });

        (await store.ExpiringWithinAsync(TimeSpan.FromDays(7))).Should().BeEmpty();

        clock.Advance(TimeSpan.FromDays(400));

        (await store.ExpiringWithinAsync(TimeSpan.FromDays(7))).Should().ContainSingle();
    }

    [Fact]
    public async Task Describing_every_secret_returns_metadata_and_no_values()
    {
        var store = Build(
            new Dictionary<string, string?> { ["Secrets:test.key"] = "a-value" },
            Requirement("test.key"));

        var descriptors = await store.DescribeAllAsync();

        descriptors.Should().ContainSingle();
        descriptors[0].Should().BeOfType<SecretDescriptor>(
            "a listing endpoint that returned values would put every secret on the platform into " +
            "one response, one log line and one screenshot");
    }

    private static SecretRequirement Requirement(string name) =>
        new(name, "Test", "A test secret.");

    private static ConfigurationSecretStore Build(
        Dictionary<string, string?> configuration, params SecretRequirement[] requirements) =>
        Build(configuration, new FakeClock(), requirements);

    private static ConfigurationSecretStore Build(
        Dictionary<string, string?> configuration,
        FakeClock clock,
        params SecretRequirement[] requirements) =>
        new(
            [new Contributor(requirements)],
            new ConfigurationBuilder().AddInMemoryCollection(configuration).Build(),
            clock,
            NullLogger<ConfigurationSecretStore>.Instance);

    private sealed class Contributor : ISecretRequirementContributor
    {
        private readonly SecretRequirement[] _requirements;

        public Contributor(SecretRequirement[] requirements) => _requirements = requirements;

        public string Module => "Test";

        public IEnumerable<SecretRequirement> Secrets => _requirements;
    }
}
