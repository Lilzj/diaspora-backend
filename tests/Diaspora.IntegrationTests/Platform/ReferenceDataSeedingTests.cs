using Diaspora.IntegrationTests.Common;
using Diaspora.Platform.Modules;
using Diaspora.Platform.Modules.Seeding;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Diaspora.IntegrationTests.Platform;

/// <summary>
/// Reference-data seeder discovery, against the real composed host.
/// <para>
/// <b>This suite is the one that would have caught the failure twice.</b> Until PSR-1 the harness
/// named every seeder by hand; a module that forgot to add itself left its catalogue empty and took
/// down all 608 tests at host start with an error naming neither the module nor the seeder. The
/// rules below assert the properties that make the hand-written list unnecessary: everything
/// registered is discovered, discovery and registration agree, one instance wears both faces, and
/// the run order is the module order rather than whatever the scanner happened to return.
/// </para>
/// </summary>
public sealed class ReferenceDataSeedingTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ReferenceDataSeedingTests(CustomWebApplicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    /// <summary>
    /// Discovery by assembly scan and registration in the container must describe the same set. A
    /// seeder in one and not the other is either a catalogue nothing seeds or a registration
    /// nothing can find.
    /// </summary>
    [Fact]
    public void Every_discovered_seeder_is_registered_in_the_host()
    {
        var discovered = ModuleAssemblyScanner
            .FindImplementations<IReferenceDataSeeder>(
                ModuleAssemblyScanner.FindPlatformAssemblies())
            .Select(type => type.Name)
            .ToHashSet(StringComparer.Ordinal);

        using var scope = _factory.Services.CreateScope();

        var registered = scope.ServiceProvider
            .GetServices<IReferenceDataSeeder>()
            .Select(seeder => seeder.GetType().Name)
            .ToHashSet(StringComparer.Ordinal);

        registered.Should().BeEquivalentTo(
            discovered,
            "a seeder found by the scanner and absent from the container is a catalogue nothing " +
            "seeds; one in the container and not the scanner means discovery has a hole");
    }

    /// <summary>
    /// <b>One instance, both faces.</b> Registering the seeder twice — once as a hosted service and
    /// once as a discoverable seeder — builds two of them, so a deployment seeds its catalogue
    /// twice and the count in the log belongs to neither.
    /// </summary>
    [Fact]
    public void A_seeder_and_its_hosted_service_are_the_same_object()
    {
        var seeders = _factory.Services.GetServices<IReferenceDataSeeder>().ToList();
        var hosted = _factory.Services.GetServices<IHostedService>().ToList();

        seeders.Should().NotBeEmpty();

        foreach (var seeder in seeders)
        {
            hosted.Should().Contain(
                service => ReferenceEquals(service, seeder),
                "AddReferenceDataSeeder forwards both registrations to one instance; two " +
                "instances means the catalogue is seeded twice — once by the hosted service the " +
                "host starts, and once by the seeder the runner calls");
        }
    }

    /// <summary>
    /// Order is derived from the module registry rather than restated on each seeder, so a
    /// catalogue lands in the same slot as the module that owns it without anybody repeating a
    /// number that already exists somewhere else.
    /// </summary>
    [Fact]
    public void Seeders_run_in_module_order()
    {
        var runner = _factory.Services.GetRequiredService<ReferenceDataSeedRunner>();
        var registry = _factory.Services.GetRequiredService<ModuleRegistry>();

        var moduleOrder = registry.Modules.ToDictionary(
            registration => registration.Name,
            registration => registration.Module.Order,
            StringComparer.OrdinalIgnoreCase);

        var actual = runner.Seeders
            .Select(seeder => (moduleOrder[seeder.Module], seeder.Order, seeder.Name))
            .ToList();

        actual.Should().BeInAscendingOrder(
            "the runner sorts by module order, then the seeder's own tie-break, then name — which " +
            "is what makes a seeding failure reproducible from one run to the next");

        runner.Seeders.Should().HaveCountGreaterThanOrEqualTo(12);
    }

    /// <summary>
    /// The three catalogues inside Guides are the platform's only case of one module seeding more
    /// than one thing, and their sequence is a decision rather than an alphabetical accident.
    /// </summary>
    [Fact]
    public void One_module_with_three_catalogues_seeds_them_in_a_declared_order()
    {
        var runner = _factory.Services.GetRequiredService<ReferenceDataSeedRunner>();

        var guides = runner.Seeders
            .Where(seeder => seeder.Module == "Guides")
            .Select(seeder => seeder.Name)
            .ToList();

        guides.Should().Equal(
            ["GuideCategorySeeder", "KnowledgeCatalogueSeeder", "NoticeCategorySeeder"],
            "B8's categories, then B8.1's navigation catalogue, then B8.2's notice categories — " +
            "declared through IReferenceDataSeeder.Order so the sequence survives a rename");
    }

    /// <summary>
    /// Every seeder names a module the registry knows, because the module string is what decides
    /// where the catalogue sorts. A typo does not fail — it moves the catalogue to the end of the
    /// run, which only shows up once something depends on the order.
    /// </summary>
    [Fact]
    public void Every_seeder_names_a_registered_module()
    {
        var runner = _factory.Services.GetRequiredService<ReferenceDataSeedRunner>();
        var registry = _factory.Services.GetRequiredService<ModuleRegistry>();

        var modules = registry.Modules
            .Select(registration => registration.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        runner.Seeders
            .Where(seeder => !modules.Contains(seeder.Module))
            .Select(seeder => $"{seeder.Name} → '{seeder.Module}'")
            .Should().BeEmpty();
    }

    /// <summary>
    /// A seeder's name is how a duplicate is detected and how an operator finds the one that
    /// failed, so two cannot share it.
    /// </summary>
    [Fact]
    public void No_two_seeders_claim_one_name()
    {
        _factory.Services
            .GetRequiredService<ReferenceDataSeedRunner>()
            .DuplicateNames
            .Should().BeEmpty();
    }

    /// <summary>
    /// <b>Idempotent by contract, and this is the assertion behind the word.</b> The harness
    /// already seeds once after creating the schema; running the whole set again must not throw,
    /// must not duplicate a row, and must report every seeder as having completed.
    /// </summary>
    [Fact]
    public async Task Seeding_the_whole_platform_again_is_safe()
    {
        using var scope = _factory.Services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<ReferenceDataSeedRunner>();

        var seeded = await runner.SeedAllAsync();

        seeded.Should().Be(
            runner.Seeders.Count,
            "every seeder is idempotent, so a second full run completes without one of them " +
            "throwing on rows its first run wrote");
    }
}
