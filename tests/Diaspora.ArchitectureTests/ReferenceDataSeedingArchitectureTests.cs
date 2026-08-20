using Diaspora.Platform.Modules;
using Diaspora.Platform.Modules.Seeding;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// PSR-1's reference-data seeding rules.
/// <para>
/// <b>Every rule here exists because the platform has already paid for its absence.</b> A module
/// that ships a catalogue and does not seed it does not fail one test: the host starts, the table
/// stays empty, and every integration test in the suite dies at host start with an error that names
/// neither the module nor the seeder. B8 recorded it, B8.1 paid it a second time, and four more
/// phases restated the registration contract from memory rather than from anything that checks.
/// </para>
/// <para>
/// These are the rules a compiled assembly can answer. The ones that need a live instance — that a
/// seeder's module exists, that no two share a name, that the run order is the module order — are
/// asserted against a booted host in <c>ReferenceDataSeedingTests</c>, because a seeder's identity
/// is something it reports rather than something its metadata declares.
/// </para>
/// </summary>
public class ReferenceDataSeedingArchitectureTests
{
    private static IReadOnlyList<Type> Seeders { get; } =
        ModuleAssemblyScanner.FindImplementations<IReferenceDataSeeder>(
            ModuleAssemblyScanner.FindPlatformAssemblies());

    /// <summary>
    /// The rules below are worthless if discovery finds nothing. Twelve seeders ship today across
    /// ten modules; the floor is the count, so a module that loses its seeder fails here rather
    /// than at the first request that needs a category.
    /// </summary>
    [Fact]
    public void Discovery_finds_every_reference_data_seeder()
    {
        Seeders.Should().HaveCountGreaterThanOrEqualTo(
            12,
            "ten modules ship a reference-data catalogue and Guides ships three; a lower count " +
            "means a seeder stopped implementing IReferenceDataSeeder and is now invisible to " +
            "the runner, the integration harness and the startup validator at once");
    }

    /// <summary>
    /// <b>The rule the old name-based sweep could not state.</b> Until PSR-1 a seeder was found by
    /// being a <c>BackgroundService</c> called <c>…Seeder</c> — and Location's
    /// <c>LocationCatalogSeedRunner</c>, the first reference-data seeder the platform ever shipped,
    /// is neither, so it had never once been checked by the rule written to protect it.
    /// </summary>
    [Fact]
    public void Every_seeder_shaped_hosted_service_is_discoverable()
    {
        var undiscoverable = ModuleAssemblyScanner
            .FindImplementations<IHostedService>(ModuleAssemblyScanner.FindPlatformAssemblies())
            .Where(type =>
                (type.Name.EndsWith("Seeder", StringComparison.Ordinal) ||
                 type.Name.EndsWith("SeedRunner", StringComparison.Ordinal)) &&
                !typeof(IReferenceDataSeeder).IsAssignableFrom(type))
            .Select(type => type.FullName)
            .ToList();

        undiscoverable.Should().BeEmpty(
            "a hosted service that seeds reference data and does not implement " +
            "IReferenceDataSeeder is invisible to the runner, so a host that applies migrations " +
            "from a separate job never seeds it — which is how a suite ends up with an empty " +
            "catalogue and no error naming it: " + string.Join(", ", undiscoverable));
    }

    /// <summary>
    /// <b>One registration call, because two would build two instances.</b> A module writing
    /// <c>AddHostedService&lt;T&gt;()</c> beside <c>AddSingleton&lt;IReferenceDataSeeder, T&gt;()</c>
    /// gets one seeder that runs at startup and a <em>different</em> one the runner calls, so the
    /// catalogue is seeded twice and the count in the log belongs to neither.
    /// </summary>
    [Fact]
    public void Every_seeder_is_registered_through_the_platform_helper()
    {
        var names = Seeders.Select(type => type.Name).ToHashSet(StringComparer.Ordinal);
        var offenders = new List<string>();

        foreach (var file in SourceTree.ProductionFiles)
        {
            foreach (var invocation in file.Root.DescendantNodes()
                .OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is not MemberAccessExpressionSyntax
                    {
                        Name: GenericNameSyntax { Identifier.ValueText: "AddHostedService" } generic
                    })
                {
                    continue;
                }

                var argument = generic.TypeArgumentList.Arguments.FirstOrDefault()?.ToString();

                if (argument is not null && names.Contains(argument))
                {
                    offenders.Add($"{file.Locate(invocation)} — {argument}");
                }
            }
        }

        offenders.Should().BeEmpty(
            "a reference-data seeder is registered with AddReferenceDataSeeder, which wires the " +
            "hosted service and the discoverable seeder to one instance. AddHostedService " +
            "registers only the first, so the runner cannot find it: " +
            string.Join(", ", offenders));
    }

    /// <summary>
    /// <b>A seeder must never take the host down.</b> A platform whose sign-in is broken because a
    /// category list was already present is a worse outcome than an incomplete catalogue — which is
    /// why the entry point is public and re-runnable, and why the integration harness can call it
    /// again once the schema exists.
    /// </summary>
    [Fact]
    public void Every_seeder_exposes_a_public_seed_entry_point()
    {
        var missing = Seeders
            .Where(type => type.GetMethod("SeedAsync", [typeof(CancellationToken)]) is null)
            .Select(type => type.Name)
            .ToList();

        missing.Should().BeEmpty(
            "a host that applies migrations from a separate job seeds again once the schema " +
            "exists, and cannot reach a seeder whose only entry point is ExecuteAsync: " +
            string.Join(", ", missing));
    }

    /// <summary>
    /// The seeder seam is the module's, not the harness's. A test project declaring its own
    /// implementation would be discovered by the kernel's scanner in any host that referenced it,
    /// which is the phantom-contributor failure the integration suite already has on record.
    /// </summary>
    [Fact]
    public void Every_seeder_lives_in_a_module_assembly()
    {
        var strays = Seeders
            .Where(type => type.Assembly.GetName().Name?
                .StartsWith("Diaspora.Modules.", StringComparison.Ordinal) != true)
            .Select(type => $"{type.Name} in {type.Assembly.GetName().Name}")
            .ToList();

        strays.Should().BeEmpty(
            "reference data belongs to the module that owns the catalogue, so its seeder lives " +
            "in that module's assembly: " + string.Join(", ", strays));
    }
}
