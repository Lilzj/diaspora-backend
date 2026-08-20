using Diaspora.IntegrationTests.Common;
using Diaspora.Platform.Modules;
using Diaspora.Platform.Operations.Alerting;
using Diaspora.Platform.Operations.Caching;
using Diaspora.Platform.Operations.Configuration;
using Diaspora.Platform.Operations.Dashboards;
using Diaspora.Platform.Operations.FeatureManagement;
using Diaspora.Platform.Operations.Health;
using Diaspora.Platform.Operations.Policies;
using Diaspora.Platform.Operations.Privacy;
using Diaspora.Platform.Operations.RateLimiting;
using Diaspora.Platform.Operations.Resilience;
using Diaspora.Platform.Operations.Scheduling;
using Diaspora.Platform.Operations.Search;
using Diaspora.Platform.Operations.Security;
using Diaspora.Platform.Operations.Workers;
using Diaspora.Platform.Services.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Platform;

/// <summary>
/// The Production Platform, inside a real composed host.
/// <para>
/// <b>These assertions exist because the unit suite cannot make them.</b> Every capability here is
/// composed from contributors discovered by an assembly scan, resolved through a container that
/// validates its own scopes, and reading a policy catalogue built from every assembly in the build.
/// A unit test builds the pieces by hand and therefore proves that the pieces work, not that the
/// platform assembled them.
/// </para>
/// <para>
/// The cache round-trip in particular. B17's own <c>CacheSerializer</c> shipped with a static
/// initializer that threw, so every cache operation on the platform failed and — because a cache
/// swallows store faults by design — reported nothing but a rising error count. A test that asserts
/// the error count is zero after real traffic is what catches that class of defect in a host.
/// </para>
/// </summary>
public sealed class ProductionPlatformTests : IntegrationTestBase
{
    public ProductionPlatformTests(CustomWebApplicationFactory factory) : base(factory)
    {
        factory.EnsureDatabaseCreated();
    }

    // ─── Composition ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_operational_policy_catalogue_composes_every_family()
    {
        using var scope = Factory.Services.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<IOperationalPolicyCatalog>();

        catalog.All.Should().NotBeEmpty();

        catalog.All.Select(policy => policy.Kind).Distinct().Should().Contain(
        [
            RetryPolicy.PolicyKind,
            CircuitBreakerPolicy.PolicyKind,
            RateLimitPolicy.PolicyKind,
            CachePolicy.PolicyKind,
            HealthPolicy.PolicyKind,
            QueuePolicy.PolicyKind,
            RecoveryPolicy.PolicyKind,
            FeatureFlagPolicy.PolicyKind
        ]);
    }

    [Fact]
    public void Every_composed_policy_passes_its_own_validation()
    {
        using var scope = Factory.Services.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<IOperationalPolicyCatalog>();

        catalog.All.SelectMany(policy => policy.Validate().Select(problem => $"{policy}: {problem}"))
            .Should().BeEmpty();
    }

    [Fact]
    public void Every_operational_capability_resolves_from_the_composed_container()
    {
        using var scope = Factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        services.GetService<ICacheService>().Should().NotBeNull();
        services.GetService<ICacheInvalidator>().Should().NotBeNull();
        services.GetService<ICacheStatisticsProvider>().Should().NotBeNull();
        services.GetService<IResilienceExecutor>().Should().NotBeNull();
        services.GetService<ICircuitBreakerRegistry>().Should().NotBeNull();
        services.GetService<IBulkheadRegistry>().Should().NotBeNull();
        services.GetService<IPlatformRateLimiter>().Should().NotBeNull();
        services.GetService<ISubjectRateLimiter>().Should().NotBeNull();
        services.GetService<IFeatureFlagService>().Should().NotBeNull();
        services.GetService<IRuntimeConfiguration>().Should().NotBeNull();
        services.GetService<IBackgroundJobScheduler>().Should().NotBeNull();
        services.GetService<IDistributedLock>().Should().NotBeNull();
        services.GetService<ISearchProvider>().Should().NotBeNull();
        services.GetService<ISecretStore>().Should().NotBeNull();
        services.GetService<IPlatformHealthAggregator>().Should().NotBeNull();
        services.GetService<IOperationalDashboardService>().Should().NotBeNull();
        services.GetService<IAlertRuleCatalog>().Should().NotBeNull();
        services.GetService<IWorkerRegistry>().Should().NotBeNull();
        services.GetService<IPrivacyAuditor>().Should().NotBeNull();
    }

    [Fact]
    public void The_kernel_ports_B17_closed_now_have_implementations_behind_them()
    {
        using var scope = Factory.Services.CreateScope();

        scope.ServiceProvider.GetService<ICacheService>().Should().NotBeNull(
            "DEBT-013: declared in B1, unimplemented until B17");

        scope.ServiceProvider.GetService<IBackgroundJobScheduler>().Should().NotBeNull(
            "DEBT-014: the same, and the port had to be rewritten before it could be implemented");
    }

    // ─── Caching ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_cache_round_trips_a_value_through_the_composed_host()
    {
        using var scope = Factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
        var key = CacheKey.For("integration", "round-trip", Guid.NewGuid().ToString("N"));

        var calls = 0;

        var first = await cache.GetOrCreateAsync(
            key, OperationalPolicyNames.Cache.Default, _ => { calls++; return Task.FromResult("value"); });

        var second = await cache.GetOrCreateAsync(
            key, OperationalPolicyNames.Cache.Default, _ => { calls++; return Task.FromResult("value"); });

        first.Should().Be("value");
        second.Should().Be("value");
        calls.Should().Be(1);
    }

    [Fact]
    public async Task The_cache_reports_no_store_errors_after_real_traffic()
    {
        using var scope = Factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
        var statistics = scope.ServiceProvider.GetRequiredService<ICacheStatisticsProvider>();

        for (var index = 0; index < 10; index++)
        {
            await cache.GetOrCreateAsync(
                CacheKey.For("integration", "errors", index.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                OperationalPolicyNames.Cache.Default,
                _ => Task.FromResult(new { Index = index }));
        }

        statistics.Snapshot().Errors.Should().Be(0,
            "the cache swallows store faults by design, so a rising error count with a healthy hit " +
            "ratio is the only signal that the whole cache is failing — which is exactly what B17's " +
            "own serializer did before this assertion existed");
    }

    [Fact]
    public async Task A_strongly_typed_identifier_survives_a_cache_round_trip()
    {
        using var scope = Factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();

        var original = new CachedShape(SharedKernel.Identity.UserId.New(), "name");
        var key = CacheKey.For("integration", "typed-id", Guid.NewGuid().ToString("N"));

        await cache.SetAsync(key, original, OperationalPolicyNames.Cache.Default);

        var read = await cache.GetAsync<CachedShape>(key, OperationalPolicyNames.Cache.Default);

        read.Should().NotBeNull();
        read!.UserId.Should().Be(original.UserId,
            "without the strongly-typed-id converter a UserId serializes as an object and comes back " +
            "as one, silently, and only in whatever code path reads the cache");
    }

    /// <summary>A shape with a strongly typed identifier, which cached values routinely contain.</summary>
    public sealed record CachedShape(SharedKernel.Identity.UserId UserId, string Name);

    // ─── Health ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_health_aggregator_runs_and_reports_a_verdict()
    {
        using var scope = Factory.Services.CreateScope();
        var aggregator = scope.ServiceProvider.GetRequiredService<IPlatformHealthAggregator>();

        var model = await aggregator.CheckAsync();

        model.Results.Should().NotBeEmpty();
        model.Instance.Should().NotBeNullOrWhiteSpace();
        model.ByDimension.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Every_health_result_carries_a_module_the_registry_knows()
    {
        using var scope = Factory.Services.CreateScope();
        var aggregator = scope.ServiceProvider.GetRequiredService<IPlatformHealthAggregator>();
        var registry = scope.ServiceProvider.GetRequiredService<ModuleRegistry>();

        var known = registry.Modules.Select(module => module.Name).Append("Platform").ToHashSet(
            StringComparer.OrdinalIgnoreCase);

        var model = await aggregator.CheckAsync();

        model.Results.Select(result => result.Module).Where(module => !known.Contains(module))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task The_readiness_endpoint_reports_the_module_health_check()
    {
        var response = await Client.GetAsync(new Uri("/health/ready", UriKind.Relative));
        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain("modules.health",
            "the aggregator is wired into readiness through one adapter, so a module's health " +
            "reaches the orchestrator without the module knowing about ASP.NET's pipeline");
    }

    // ─── Feature management and configuration ────────────────────────────────────────────

    [Fact]
    public async Task Feature_flags_resolve_and_an_undeclared_one_is_off()
    {
        using var scope = Factory.Services.CreateScope();
        var flags = scope.ServiceProvider.GetRequiredService<IFeatureFlagService>();

        (await flags.IsEnabledAsync("nothing.declared", default)).Should().BeFalse();
    }

    [Fact]
    public async Task The_kill_switch_can_be_engaged_and_released_in_a_composed_host()
    {
        using var scope = Factory.Services.CreateScope();
        var administration = scope.ServiceProvider.GetRequiredService<IFeatureFlagAdministration>();

        try
        {
            await administration.EngageKillSwitchAsync("integration-test", "verifying the switch");

            (await administration.GetKillSwitchAsync()).IsEngaged.Should().BeTrue();
        }
        finally
        {
            // Released in a finally because the flag service is a singleton: a kill switch left
            // engaged would silently disable flags for every test that ran afterwards.
            await administration.ReleaseKillSwitchAsync("integration-test");
        }

        (await administration.GetKillSwitchAsync()).IsEngaged.Should().BeFalse();
    }

    [Fact]
    public void Runtime_configuration_composes_and_every_declared_default_validates()
    {
        using var scope = Factory.Services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IRuntimeConfiguration>();
        var administration = scope.ServiceProvider.GetRequiredService<IRuntimeConfigurationAdministration>();

        foreach (var entry in configuration.All)
        {
            administration.Validate(entry.Definition.Key, entry.Definition.DefaultValue)
                .IsValid.Should().BeTrue(
                    $"'{entry.Definition.Key}' declares a default that does not validate, which leaves " +
                    "the setting with no valid value at any layer");
        }
    }

    // ─── Workers, scheduling and dashboards ──────────────────────────────────────────────

    [Fact]
    public async Task The_platforms_workers_start_and_report_themselves()
    {
        using var scope = Factory.Services.CreateScope();
        var workers = scope.ServiceProvider.GetRequiredService<IWorkerRegistry>();

        // The registry is populated as each worker's loop starts, which happens after the host
        // reports started. A short wait rather than an immediate assertion, because asserting on a
        // background service the instant the host is up is a race the test would lose most of the time.
        for (var attempt = 0; attempt < 40 && workers.All.Count == 0; attempt++)
        {
            await Task.Delay(50);
        }

        workers.All.Should().NotBeEmpty(
            "a worker registered in the container and never started is precisely the failure the " +
            "registry exists to surface");

        workers.All.Should().OnlyContain(
            worker => worker.Lifecycle != WorkerLifecycle.Faulted);
    }

    [Fact]
    public async Task A_job_can_be_scheduled_and_found()
    {
        using var scope = Factory.Services.CreateScope();
        var scheduler = scope.ServiceProvider.GetRequiredService<IBackgroundJobScheduler>();

        var id = await scheduler.ScheduleAsync("integration.test.job", TimeSpan.FromHours(1));

        var job = await scheduler.FindAsync(id);

        job.Should().NotBeNull();
        job!.Kind.Should().Be(JobKind.Delayed);
        job.Status.Should().Be(JobStatus.Pending);
    }

    [Fact]
    public async Task A_job_with_no_handler_is_dead_lettered_rather_than_retried_forever()
    {
        using var scope = Factory.Services.CreateScope();
        var scheduler = scope.ServiceProvider.GetRequiredService<IBackgroundJobScheduler>();

        var id = await scheduler.EnqueueAsync("integration.no.such.handler");

        ScheduledJob? job = null;

        for (var attempt = 0; attempt < 60; attempt++)
        {
            job = await scheduler.FindAsync(id);

            if (job is { Status: JobStatus.DeadLettered })
            {
                break;
            }

            await Task.Delay(100);
        }

        job!.Status.Should().Be(JobStatus.DeadLettered,
            "retrying a job whose handler does not exist would take the poison threshold's worth of " +
            "attempts to reach the same answer, and would look like a failing dependency all the way");
    }

    [Fact]
    public async Task Every_declared_dashboard_builds()
    {
        using var scope = Factory.Services.CreateScope();
        var dashboards = scope.ServiceProvider.GetRequiredService<IOperationalDashboardService>();

        dashboards.Available.Should().NotBeEmpty();

        foreach (var name in dashboards.Available)
        {
            var board = await dashboards.BuildAsync(name);

            board.Should().NotBeNull();
            board!.Panels.Should().NotBeEmpty($"'{name}' has a contributor, so it should have panels");
        }
    }

    [Fact]
    public void Every_alert_rule_names_a_metric_and_carries_a_runbook()
    {
        using var scope = Factory.Services.CreateScope();
        var alerts = scope.ServiceProvider.GetRequiredService<IAlertRuleCatalog>();

        alerts.All.Should().NotBeEmpty();
        alerts.All.Should().OnlyContain(rule =>
            !string.IsNullOrWhiteSpace(rule.Metric) &&
            !string.IsNullOrWhiteSpace(rule.Runbook) &&
            rule.For > TimeSpan.Zero);
    }

    // ─── Security and privacy ────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_secret_store_validates_without_returning_a_value()
    {
        using var scope = Factory.Services.CreateScope();
        var secrets = scope.ServiceProvider.GetRequiredService<ISecretStore>();

        var descriptors = await secrets.DescribeAllAsync();

        descriptors.Should().OnlyContain(descriptor => descriptor.Fingerprint.Length == 12);

        // No assertion on the problem list. Which secrets a deployment declares is a deployment's
        // business, and asserting on the count here would make this test fail the day a module
        // declares one.
        (await secrets.ValidateAsync()).Should().NotBeNull();
    }

    [Fact]
    public async Task The_privacy_audit_covers_every_registered_module()
    {
        using var scope = Factory.Services.CreateScope();
        var auditor = scope.ServiceProvider.GetRequiredService<IPrivacyAuditor>();
        var registry = scope.ServiceProvider.GetRequiredService<ModuleRegistry>();

        var entries = await auditor.AuditAsync();

        entries.Select(entry => entry.Module).Distinct()
            .Should().HaveCount(registry.Modules.Count,
                "the findings are the point: a module with no erasure provider cannot honour a " +
                "deletion request, and nothing else would surface that until one arrived");
    }

    // ─── Resilience ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_resilience_executor_runs_an_operation_and_records_its_breaker()
    {
        using var scope = Factory.Services.CreateScope();
        var executor = scope.ServiceProvider.GetRequiredService<IResilienceExecutor>();
        var breakers = scope.ServiceProvider.GetRequiredService<ICircuitBreakerRegistry>();

        var result = await executor.ExecuteAsync(
            OperationalPolicyNames.CircuitBreaker.ExternalApi, _ => Task.FromResult(42));

        result.Should().Be(42);

        breakers.Find(OperationalPolicyNames.CircuitBreaker.ExternalApi).Should().NotBeNull(
            "every call through the executor is visible to the dashboard, which is the whole reason " +
            "retry and breaking are centralised");
    }

    [Fact]
    public async Task An_isolated_circuit_refuses_without_calling_the_dependency()
    {
        using var scope = Factory.Services.CreateScope();
        var executor = scope.ServiceProvider.GetRequiredService<IResilienceExecutor>();
        var breakers = scope.ServiceProvider.GetRequiredService<ICircuitBreakerRegistry>();

        const string dependency = "integration.isolated";

        breakers.Isolate(dependency);

        try
        {
            var called = false;

            var call = async () => await executor.ExecuteAsync(
                dependency, _ => { called = true; return Task.FromResult(1); });

            await call.Should().ThrowAsync<CircuitOpenException>();

            called.Should().BeFalse();
        }
        finally
        {
            // The registry is a singleton for the host's lifetime, so an isolated circuit left behind
            // would refuse every later test that touched the same dependency name.
            breakers.Reset(dependency);
        }
    }

    // ─── Search ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_search_provider_reports_that_it_is_not_configured_rather_than_matching_nothing()
    {
        using var scope = Factory.Services.CreateScope();
        var search = scope.ServiceProvider.GetRequiredService<ISearchProvider>();

        (await search.IsAvailableAsync()).Should().BeFalse();
        search.Name.Should().Be("none",
            "a provider that returned an empty result set instead would look like 'nothing matched', " +
            "and the platform would report zero results for every search on the day somebody forgot " +
            "to register a real one");
    }
}
