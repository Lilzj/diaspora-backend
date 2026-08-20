using Diaspora.Platform.Modules;
using Diaspora.Platform.Modules.Seeding;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.UnitTests.Platform;

/// <summary>
/// The runner that replaced a hand-written list of twelve seeders.
/// <para>
/// Three properties matter and each has a failure behind it. <b>Deterministic order</b>, because a
/// catalogue that another module's seeder reads must exist by then, and because a seeding failure
/// nobody can reproduce is a seeding failure nobody fixes. <b>Duplicate detection</b>, because two
/// seeders answering to one name make every log line about either of them ambiguous.
/// <b>Failure isolation</b>, because one catalogue that cannot be written must not cost the platform
/// the eleven that can — and must never take the host down, which is the contract every individual
/// seeder has held since B4.
/// </para>
/// </summary>
public class ReferenceDataSeedRunnerTests
{
    [Fact]
    public void Seeders_run_in_module_order_rather_than_discovery_order()
    {
        var runner = new ReferenceDataSeedRunner(
            [
                new StubSeeder("Last", "Recommendations"),
                new StubSeeder("First", "Location"),
                new StubSeeder("Middle", "Guides")
            ],
            Registry(("Location", 300), ("Guides", 700), ("Recommendations", 1300)));

        runner.Seeders.Select(seeder => seeder.Name)
            .Should().Equal("First", "Middle", "Last");
    }

    /// <summary>
    /// A module's own order is a fact the registry holds; the seeder's <c>Order</c> only breaks
    /// ties inside it. Guides' three catalogues are the platform's only real case.
    /// </summary>
    [Fact]
    public void A_seeders_own_order_breaks_ties_within_one_module()
    {
        var runner = new ReferenceDataSeedRunner(
            [
                new StubSeeder("Notices", "Guides", Order: 20),
                new StubSeeder("Categories", "Guides", Order: 0),
                new StubSeeder("Knowledge", "Guides", Order: 10)
            ],
            Registry(("Guides", 700)));

        runner.Seeders.Select(seeder => seeder.Name)
            .Should().Equal("Categories", "Knowledge", "Notices");
    }

    /// <summary>
    /// Name is the last tie-break, so two seeders in one module at one order still run in the same
    /// sequence on every boot rather than in whatever order the assembly scanner returned.
    /// </summary>
    [Fact]
    public void Name_is_the_final_tie_break()
    {
        var runner = new ReferenceDataSeedRunner(
            [new StubSeeder("Beta", "Guides"), new StubSeeder("Alpha", "Guides")],
            Registry(("Guides", 700)));

        runner.Seeders.Select(seeder => seeder.Name).Should().Equal("Alpha", "Beta");
    }

    /// <summary>
    /// <b>A seeder attributed to a module nobody registered still seeds.</b> It sorts last, because
    /// an unknown module name is a metadata mistake the startup validator reports — and dropping
    /// the catalogue would turn a typo into a module that cannot serve a single request.
    /// </summary>
    [Fact]
    public void A_seeder_naming_an_unknown_module_sorts_last_and_is_still_run()
    {
        var runner = new ReferenceDataSeedRunner(
            [new StubSeeder("Orphan", "Typo"), new StubSeeder("Known", "Guides")],
            Registry(("Guides", 700)));

        runner.Seeders.Select(seeder => seeder.Name).Should().Equal("Known", "Orphan");
    }

    /// <summary>
    /// With no registry — a unit-test host that never composed one — ordering falls back to the
    /// seeder's own order and its name. Still deterministic, which is the property that matters.
    /// </summary>
    [Fact]
    public void Ordering_without_a_module_registry_is_still_deterministic()
    {
        var runner = new ReferenceDataSeedRunner(
            [new StubSeeder("Zulu", "Guides"), new StubSeeder("Alpha", "Location")]);

        runner.Seeders.Select(seeder => seeder.Name).Should().Equal("Alpha", "Zulu");
    }

    [Fact]
    public void Two_seeders_sharing_a_name_are_reported()
    {
        var runner = new ReferenceDataSeedRunner(
            [
                new StubSeeder("Categories", "Guides"),
                new StubSeeder("Categories", "Jobs"),
                new StubSeeder("Policies", "Recommendations")
            ]);

        runner.DuplicateNames.Should().Equal("Categories");
    }

    [Fact]
    public void Distinct_names_report_no_duplicates()
    {
        var runner = new ReferenceDataSeedRunner(
            [new StubSeeder("Categories", "Guides"), new StubSeeder("Policies", "Jobs")]);

        runner.DuplicateNames.Should().BeEmpty();
    }

    [Fact]
    public async Task Every_seeder_runs()
    {
        var first = new StubSeeder("First", "Location");
        var second = new StubSeeder("Second", "Guides");

        var seeded = await new ReferenceDataSeedRunner([first, second]).SeedAllAsync();

        seeded.Should().Be(2);
        first.Invocations.Should().Be(1);
        second.Invocations.Should().Be(1);
    }

    /// <summary>
    /// <b>The rule that keeps a bad catalogue from becoming a dead platform.</b> One seeder throwing
    /// must not stop the ones after it, and must not surface as an exception the host start would
    /// fail on.
    /// </summary>
    [Fact]
    public async Task A_failing_seeder_does_not_stop_the_others()
    {
        var before = new StubSeeder("Aaa", "Location");
        var failing = new StubSeeder("Bbb", "Guides", Fails: true);
        var after = new StubSeeder("Ccc", "Recommendations");

        var runner = new ReferenceDataSeedRunner([before, failing, after]);

        var seeded = await runner.SeedAllAsync();

        seeded.Should().Be(2, "the two that worked are reported; the one that threw is logged");
        before.Invocations.Should().Be(1);
        after.Invocations.Should().Be(1);
    }

    [Fact]
    public async Task Seeding_nothing_is_not_an_error()
    {
        var seeded = await new ReferenceDataSeedRunner([]).SeedAllAsync();

        seeded.Should().Be(0);
    }

    /// <summary>
    /// The kernel composes the runner from whatever the modules registered, so a host with no
    /// seeders at all resolves one that does nothing rather than failing to resolve.
    /// </summary>
    [Fact]
    public void The_kernel_registers_a_runner()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPlatformKernel(
            new ConfigurationBuilder().Build(),
            [typeof(ReferenceDataSeedRunner).Assembly]);

        using var provider = services.BuildServiceProvider();

        provider.GetService<ReferenceDataSeedRunner>().Should().NotBeNull();
    }

    /// <summary>
    /// A registry holding the four stub modules below, filtered to the ones a test names. Their
    /// orders mirror the real platform's — Location 300, Guides 700, Jobs 800,
    /// Recommendations 1300 — so a test reads the way the boot log does.
    /// </summary>
    private static ModuleRegistry Registry(params (string Name, int Order)[] modules)
    {
        var wanted = modules.Select(module => module.Name).ToHashSet(StringComparer.Ordinal);
        var registry = new ModuleRegistry();

        if (wanted.Contains("Location")) registry.Register<LocationStubModule>();
        if (wanted.Contains("Guides")) registry.Register<GuidesStubModule>();
        if (wanted.Contains("Jobs")) registry.Register<JobsStubModule>();
        if (wanted.Contains("Recommendations")) registry.Register<RecommendationsStubModule>();

        return registry;
    }

    private sealed record StubSeeder(
        string Name, string Module, int Order = 0, bool Fails = false) : IReferenceDataSeeder
    {
        public int Invocations { get; private set; }

        public Task SeedAsync(CancellationToken cancellationToken = default)
        {
            Invocations++;

            return Fails
                ? Task.FromException(new InvalidOperationException("no such table"))
                : Task.CompletedTask;
        }
    }

    private abstract class StubModule : IPlatformModule
    {
        public abstract string Name { get; }
        public abstract int Order { get; }
        public string Version => "1.0.0";

        public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
        }
    }

    private sealed class LocationStubModule : StubModule
    {
        public override string Name => "Location";
        public override int Order => 300;
    }

    private sealed class GuidesStubModule : StubModule
    {
        public override string Name => "Guides";
        public override int Order => 700;
    }

    private sealed class JobsStubModule : StubModule
    {
        public override string Name => "Jobs";
        public override int Order => 800;
    }

    private sealed class RecommendationsStubModule : StubModule
    {
        public override string Name => "Recommendations";
        public override int Order => 1300;
    }
}
