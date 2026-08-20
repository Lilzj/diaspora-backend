using System.Reflection;
using Diaspora.Platform.Modules;
using FluentAssertions;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// The module graph, expressed once instead of module by module.
/// <para>
/// <b>Every vertical already has its own dependency test, and between them they still leave
/// holes.</b> Location asserts it does not depend on any vertical, Media asserts the same, and
/// Community asserts it against three of the six that existed when it was written. The rule they
/// are all approximating is one sentence — <em>a module never depends on a module composed after
/// it</em> — and stating it once means the module that lands next is covered before anybody
/// remembers to add it.
/// </para>
/// <para>
/// <b>Composition order is the layering, and it is already declared.</b> Every module publishes an
/// <c>Order</c> so the kernel can compose services in a sequence where a later module can replace
/// an earlier one's default. That is a dependency direction whether or not anybody called it one:
/// Trust at 200, Location at 300, Media at 400, Community at 500, Providers at 600, Guides at 700,
/// then the six verticals, then Communication and Recommendation last with no module reference
/// between them. Reading the layering off the number nobody can forget to update beats maintaining
/// a second list that says the same thing.
/// </para>
/// </summary>
public sealed class ModuleDependencyGuardrailArchitectureTests
{
    private static readonly IReadOnlyList<ModuleRegistration> Modules =
        new ModuleRegistry().DiscoverModules().Modules;

    /// <summary>The assembly each module ships in, by module name.</summary>
    private static readonly Dictionary<string, ModuleRegistration> ByAssembly =
        Modules.ToDictionary(
            registration => registration.Module.GetType().Assembly.GetName().Name!,
            registration => registration,
            StringComparer.Ordinal);

    /// <summary>
    /// The composition root, which is not a module and must not be read as one.
    /// <para>
    /// <b>Identity ships inside the host assembly</b>, so every reference <c>Diaspora.Api</c> holds is
    /// attributed by this test to the Identity module. The host has held a project reference to all
    /// fifteen modules since B3 — that is what a composition root is — and the forward-reference rule
    /// below is about modules, not about it.
    /// </para>
    /// <para>
    /// <b>B17 found that the rule had been passing by accident.</b> <c>GetReferencedAssemblies</c>
    /// reports what the compiler emitted, and the compiler elides a reference whose types are never
    /// used — so the host's fifteen references were invisible here for as long as <c>Program.cs</c>
    /// touched no module type. The first line of host code that did (a cache bridge B17 wrote and
    /// later withdrew for unrelated reasons) made two of them appear, and the rule reported Identity
    /// depending on Recommendation and Financial.
    /// <b>The trigger was removed; the correction stands.</b> Reverting it would restore an accidental
    /// pass — a rule that has never failed may never have run — and the next host line to touch a
    /// module type would rediscover the same artefact. Excluding the root is a correction to the
    /// attribution, not a relaxation of the rule: every real module assembly is still checked, and the
    /// sibling test below still forbids a module from naming the host.
    /// </para>
    /// </summary>
    private const string CompositionRoot = "Diaspora.Api";

    [Fact]
    public void The_module_scan_finds_the_platforms_modules()
    {
        Modules.Should().HaveCountGreaterThanOrEqualTo(
            14, "fifteen modules have shipped between B2 and B14");

        Modules.Select(registration => registration.Module.Order)
            .Should().OnlyHaveUniqueItems(
                "two modules sharing an order compose in whichever sequence the scan produced");
    }

    /// <summary>
    /// <b>The rule.</b> A module may reference a module composed before it and never one composed
    /// after. Referencing forwards is a cycle waiting for the second reference, and it means the
    /// referenced module's defaults are registered after the referencing module has already
    /// depended on them.
    /// </summary>
    [Fact]
    public void No_module_depends_on_a_module_composed_after_it()
    {
        var offenders = new List<string>();

        foreach (var (assemblyName, module) in ByAssembly)
        {
            if (string.Equals(assemblyName, CompositionRoot, StringComparison.Ordinal))
            {
                continue;
            }

            var referenced = module.Module.GetType().Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .Where(name => name is not null && ByAssembly.ContainsKey(name))
                .Select(name => ByAssembly[name!]);

            foreach (var dependency in referenced)
            {
                if (dependency.Module.Order > module.Module.Order)
                {
                    offenders.Add(
                        $"{module.Name} ({module.Module.Order}) references " +
                        $"{dependency.Name} ({dependency.Module.Order})");
                }
            }
        }

        offenders.Should().BeEmpty(
            "the module graph is acyclic and composition order is its direction:\n" +
            string.Join("\n", offenders));
    }

    /// <summary>
    /// <b>No module reaches the host or its adapters.</b> A module referencing the API project
    /// would make the host un-splittable and the module un-testable without one; a module
    /// referencing Infrastructure would bind a vertical to a concrete storage, mail or token
    /// implementation the platform exists to keep swappable.
    /// </summary>
    [Theory]
    [InlineData("Diaspora.Api")]
    [InlineData("Diaspora.Infrastructure")]
    [InlineData("Diaspora.Observability")]
    public void No_module_depends_on_the_host_or_its_adapters(string forbidden)
    {
        var offenders = ModuleAssemblies()
            .Where(assembly => assembly.GetReferencedAssemblies()
                .Any(reference => reference.Name == forbidden))
            .Select(assembly => assembly.GetName().Name!)
            .ToList();

        offenders.Should().BeEmpty(
            $"'{forbidden}' is the composition root or an adapter, and a module that names one " +
            "cannot be deployed, tested or replaced on its own: " + string.Join(", ", offenders));
    }

    /// <summary>
    /// <b>The two newest modules reference no vertical at all, and that is their central claim.</b>
    /// Communication assembles every notification from a published contract's payload and
    /// Recommendation assembles every candidate the same way. A single project reference added to
    /// render one extra field would undo it, and would pass code review as a convenience.
    /// </summary>
    [Theory]
    [InlineData("Diaspora.Modules.Notifications")]
    [InlineData("Diaspora.Modules.Recommendations")]
    public void The_consumer_modules_reference_no_module_at_all(string consumer)
    {
        var assembly = ModuleAssemblies()
            .Single(candidate => candidate.GetName().Name == consumer);

        var referenced = assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name?.StartsWith("Diaspora.Modules.", StringComparison.Ordinal) == true)
            .Where(name => name != consumer)
            .ToList();

        referenced.Should().BeEmpty(
            $"{consumer} consumes published contracts and nothing else: " +
            string.Join(", ", referenced));
    }

    /// <summary>
    /// Every module reaches another module's <em>facts</em> through <c>Diaspora.Contracts</c>. The
    /// reference itself is not the violation — the platform-service modules are referenced directly
    /// and deliberately — but a module that consumes events must have the contracts assembly to
    /// consume them from, and one that does not is reaching for the domain type instead.
    /// </summary>
    [Fact]
    public void Every_module_that_consumes_events_references_the_contracts_assembly()
    {
        var offenders = ModuleAssemblies()
            .Where(assembly => assembly.GetTypes().Any(type =>
                type.GetInterfaces().Any(contract =>
                    contract.IsGenericType &&
                    contract.GetGenericTypeDefinition() == typeof(MediatR.INotificationHandler<>) &&
                    contract.GetGenericArguments()[0].Name.StartsWith(
                        "IntegrationEventNotification", StringComparison.Ordinal))))
            .Where(assembly => !assembly.GetReferencedAssemblies()
                .Any(reference => reference.Name == "Diaspora.Contracts"))
            .Select(assembly => assembly.GetName().Name!)
            .ToList();

        offenders.Should().BeEmpty(
            "a consumer reads a frozen contract, never a producer's domain type: " +
            string.Join(", ", offenders));
    }

    private static IEnumerable<Assembly> ModuleAssemblies() =>
        ModuleAssemblyScanner.FindPlatformAssemblies()
            .Where(assembly => assembly.GetName().Name?.StartsWith(
                "Diaspora.Modules.", StringComparison.Ordinal) == true);
}
