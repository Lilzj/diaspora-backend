using System.Reflection;
using Diaspora.Platform.Modules;
using Diaspora.Platform.Modules.Contributors;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// The rules PSR-1 added, each closing a recommendation an earlier phase wrote down and no phase
/// implemented.
/// <para>
/// <b>Every one of them passed on its first run against the real platform</b>, which is the outcome
/// the platform's own guidance asks for — an architecture rule's first run is a measurement, and a
/// rule that has to change the code to pass is a rule that needs its verdict examined first. These
/// are ratchets: they cost nothing today and fail the moment the recommendation's subject recurs.
/// </para>
/// </summary>
public class PsrStabilizationArchitectureTests
{
    /// <summary>
    /// <b>B6 · N-2, recommended and never built.</b> Three Community tables carry a
    /// <c>community_</c> prefix because <c>community.bookmarks</c> and <c>bookmarks.bookmarks</c>
    /// collide under SQLite, which ignores schemas — and SQLite is the provider every integration
    /// test runs on. The failure therefore appears only in tests, which is the worst place for a
    /// name clash to surface: it looks like a broken test rather than a modelling decision.
    /// <para>
    /// B6 asked for "a startup check that no two modules map the same table name, not just the same
    /// schema". This is that check, moved earlier still — a build failure rather than a boot
    /// warning, because the model it reads is fixed at compile time.
    /// </para>
    /// </summary>
    [Fact]
    public void No_two_schemas_map_one_table_name()
    {
        var collisions = PlatformModel.Value.GetEntityTypes()
            .Where(entity => entity.GetTableName() is not null)
            .Select(entity => (Table: entity.GetTableName()!, Schema: entity.GetSchema() ?? "(none)"))
            .Distinct()
            .GroupBy(pair => pair.Table, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Select(pair => pair.Schema).Distinct().Count() > 1)
            .Select(group => $"{group.Key} in {string.Join(" and ", group.Select(p => p.Schema))}")
            .ToList();

        collisions.Should().BeEmpty(
            "SQLite ignores schemas, so two modules mapping one table name share a table under " +
            "the provider the whole integration suite runs on — and the failure names a test " +
            "rather than the modules that collided. Prefix the table, as Community did for " +
            "bookmarks: " + string.Join("; ", collisions));
    }

    /// <summary>
    /// The rule above is worthless if the model is empty or partial, which is exactly how an
    /// earlier model-reading test exempted two modules for four phases without failing.
    /// </summary>
    [Fact]
    public void The_model_the_collision_rule_reads_covers_the_whole_platform()
    {
        PlatformModel.Value.GetEntityTypes().Should().HaveCountGreaterThanOrEqualTo(
            250, "the platform maps around 290 tables; a much smaller model is a partial one");
    }

    /// <summary>
    /// <b>B1.3's third recommendation for B12, never taken up.</b> The existing rule covers value
    /// types, because that is where both recorded defects were — <c>bool IncludeFacets</c> made the
    /// plainest search link on the platform a 400 with an empty body. Reference-type nullability
    /// drives the same binding behaviour: a non-nullable <c>string</c> on an <c>[AsParameters]</c>
    /// request is a required query parameter, and omitting it produces the same silent 400.
    /// <para>
    /// Read through <see cref="NullabilityInfoContext"/> rather than by looking for
    /// <c>?</c>, because what decides the binding is the compiler's annotation and nothing else.
    /// </para>
    /// </summary>
    [Fact]
    public void Every_as_parameters_reference_property_is_nullable_or_declared_required()
    {
        var nullability = new NullabilityInfoContext();
        var offenders = new List<string>();

        foreach (var type in AsParametersTypes())
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.PropertyType.IsValueType ||
                    property.GetCustomAttribute<
                        System.ComponentModel.DataAnnotations.RequiredAttribute>() is not null ||
                    nullability.Create(property).WriteState != NullabilityState.NotNull)
                {
                    continue;
                }

                offenders.Add($"{type.Name}.{property.Name} ({property.PropertyType.Name})");
            }
        }

        offenders.Should().BeEmpty(
            "a non-nullable reference type on an [AsParameters] request binds as a required " +
            "query parameter, and leaving it out answers with an empty 400 — no validation " +
            "message, nothing in the logs above a debug line. Make it nullable, or attribute it " +
            "[Required] to say you meant it: " + string.Join(", ", offenders));
    }

    /// <summary>
    /// <b>B1.4.1 · F8, closed here.</b> The contributor analyzers resolve a catalogue name through
    /// the semantic model at the <c>new</c> site. A definition built inside a private factory that
    /// takes the name as a parameter therefore resolves to nothing, the analyzer skips it, and
    /// every key in that file is exempt from three naming rules — silently, and without a
    /// suppression anybody could review. B14 did exactly that for thirteen keys and nobody knew
    /// until B1.4.1 counted the composed catalogue and found the totals disagreed.
    /// <para>
    /// B1.4.1 named two ways to close it and preferred this one: adopt the convention that
    /// definitions are constructed inline, and assert it. PSR-1 rewrote B14's contributor and this
    /// is the assertion.
    /// </para>
    /// <para>
    /// The kernel's own <c>PolicyDefinition.ForPermission</c> and <c>ForTier</c> are excluded
    /// because they are not a blind spot: <c>ContributorConventionAnalyzer.AnalyzeFactory</c>
    /// resolves both by name, which is why the platform's policy names have always been checked.
    /// </para>
    /// </summary>
    [Fact]
    public void No_catalogue_definition_hides_its_name_behind_a_factory()
    {
        string[] definitions = ["PermissionDefinition", "FeatureDefinition", "PolicyDefinition"];
        string[] analyzerAware = ["ForPermission", "ForTier"];

        var offenders = new List<string>();

        foreach (var file in SourceTree.ProductionFiles)
        {
            foreach (var method in file.Root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                if (!definitions.Contains(method.ReturnType.ToString()) ||
                    analyzerAware.Contains(method.Identifier.ValueText))
                {
                    continue;
                }

                var parameters = method.ParameterList.Parameters
                    .Select(parameter => parameter.Identifier.ValueText)
                    .ToHashSet(StringComparer.Ordinal);

                foreach (var creation in method.DescendantNodes()
                    .OfType<BaseObjectCreationExpressionSyntax>())
                {
                    if (creation.ArgumentList?.Arguments.FirstOrDefault()?.Expression
                            is IdentifierNameSyntax identifier &&
                        parameters.Contains(identifier.Identifier.ValueText))
                    {
                        offenders.Add(
                            $"{file.Locate(creation)} — {method.ReturnType} {method.Identifier}");
                    }
                }
            }
        }

        offenders.Should().BeEmpty(
            "DSP5002, DSP5004 and DSP5006 read the catalogue name at the `new` site through the " +
            "semantic model. A factory that takes the name as a parameter makes it a parameter " +
            "there, so all three skip the declaration and the module's keys are exempt from the " +
            "naming rules without anybody deciding they should be. Construct the definition " +
            "inline: " + string.Join("; ", offenders));
    }

    /// <summary>
    /// <b>No kernel port is dormant.</b> B2.6 recorded six unimplemented kernel abstractions, B5
    /// recorded two of them again, B7 a third, B13 a fourth and B14 a fifth. Four were answered
    /// before PSR-1 — <c>IStorageService</c> and <c>IImageProcessingService</c> by Media in B5,
    /// <c>INotificationPublisher</c> by B1.4.1's obsoletion, and the caching question by B13 and B14
    /// each declaring their own port with a working default behind it.
    /// <para>
    /// <b>B17 closed the last two.</b> <c>ICacheService</c> (DEBT-013) and
    /// <c>IBackgroundJobScheduler</c> (DEBT-014) are implemented by the Production Platform, and both
    /// were rewritten in the same commit — neither shape could have been implemented as declared, which
    /// is part of why five phases went around them rather than through them.
    /// </para>
    /// <para>
    /// The assertion is now that the set is <em>empty</em>, and that is the stronger form of the same
    /// rule: a kernel port with nothing behind it reads as available and is not, and B13 recorded what
    /// that costs — a component that depends on one fails in silence. A new dormant port fails here on
    /// the day it is declared rather than on the day something depends on it.
    /// </para>
    /// </summary>
    [Fact]
    public void No_kernel_port_is_dormant()
    {
        var assemblies = ModuleAssemblyScanner.FindPlatformAssemblies();
        var implementations = ModuleAssemblyScanner.FindImplementations<object>(assemblies);

        var dormant = KernelPorts()
            .Where(port =>
                port.GetCustomAttribute<ObsoleteAttribute>() is null &&
                !implementations.Any(type => port.IsAssignableFrom(type) && type != port))
            .Select(port => port.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        dormant.Should().BeEmpty(
            "every kernel port now has an implementation behind it. Declaring one without is the " +
            "state B2.6 recorded and five later phases worked around; if a new port needs to be " +
            "declared ahead of its implementation, ship the implementation in the same change or " +
            "declare the port in the module that owns it: " + string.Join(", ", dormant));
    }

    /// <summary>
    /// The counterpart, and the rule that actually protects anybody. A dormant port is harmless
    /// until something injects it — <b>which is the failure B13 recorded and B14 designed around</b>
    /// by declaring its own cache port with a working in-memory default behind it. The pattern is
    /// the platform's answer: a module needing a capability the kernel declares and does not
    /// implement declares its own and ships a default.
    /// </summary>
    [Fact]
    public void No_module_depends_on_a_dormant_kernel_port()
    {
        var assemblies = ModuleAssemblyScanner.FindPlatformAssemblies();
        var implementations = ModuleAssemblyScanner.FindImplementations<object>(assemblies);

        var dormant = KernelPorts()
            .Where(port =>
                port.GetCustomAttribute<ObsoleteAttribute>() is null &&
                !implementations.Any(type => port.IsAssignableFrom(type) && type != port))
            .ToHashSet();

        var dependents = implementations
            .Where(type => type.Assembly.GetName().Name?
                .StartsWith("Diaspora.Modules.", StringComparison.Ordinal) == true)
            .SelectMany(type => type
                .GetConstructors()
                .SelectMany(constructor => constructor.GetParameters())
                .Where(parameter => dormant.Contains(Unwrap(parameter.ParameterType)))
                .Select(parameter => $"{type.Name}({Unwrap(parameter.ParameterType).Name})"))
            .Distinct()
            .ToList();

        dependents.Should().BeEmpty(
            "a module that injects a port nothing implements gets a container that cannot build " +
            "it, or — if it asks for the enumerable — an empty one and a component that does " +
            "nothing without saying so. Declare a module-owned port and ship a default behind it, " +
            "as B13 and B14 both did for caching: " + string.Join(", ", dependents));
    }

    /// <summary>
    /// <b>The provisional catalogue shrinks and never grows.</b> B2 declared seven permissions and
    /// gates before the modules that own them existed, and the host has carried them attributed to
    /// "Platform (provisional)" ever since — a string chosen so anything wearing it reads as debt.
    /// <para>
    /// PSR-1's continuation moved the three whose owning module both exists <em>and</em> shares
    /// their namespace. The four that remain cannot move, and the rule pins them so a later module
    /// cannot quietly add an eighth.
    /// </para>
    /// <para>
    /// <b>The reason they cannot move is sharper than "the module does not exist yet", and PSR-1
    /// found it by trying.</b> A gate can only leave this catalogue if its name already matches its
    /// owner's namespace. <c>reviews.post</c> names a bounded context that never became a module;
    /// Providers owns reviews and its own rule requires every gate it declares to start with
    /// <c>providers.</c>, so B7 cannot declare it — and the name cannot change, because the client
    /// switches on the string. <c>content:publish</c> is worse: <b>nothing enforces it anywhere</b>,
    /// so moving it would invent an owner. The other two name a Messaging module that does not
    /// exist.
    /// </para>
    /// <para>
    /// The count is the whole assertion. A module landing must delete its placeholder in the same
    /// commit, because the feature catalogue refuses two modules declaring one gate and the
    /// resulting startup failure takes down every request that resolves it — phone verification
    /// included. B8, B9, B10 and B12 each record that trap.
    /// </para>
    /// </summary>
    [Fact]
    public void The_provisional_catalogue_holds_only_the_entries_that_cannot_move()
    {
        var provisional = ModuleAssemblyScanner
            .CreateAll<IPermissionContributor>(ModuleAssemblyScanner.FindPlatformAssemblies())
            .Where(contributor => contributor.Module.Contains("provisional", StringComparison.OrdinalIgnoreCase))
            .SelectMany(contributor => contributor.GetPermissions().Select(permission => permission.Key))
            .Concat(ModuleAssemblyScanner
                .CreateAll<IFeatureContributor>(ModuleAssemblyScanner.FindPlatformAssemblies())
                .Where(contributor => contributor.Module.Contains("provisional", StringComparison.OrdinalIgnoreCase))
                .SelectMany(contributor => contributor.GetFeatures().Select(feature => feature.Name)))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        provisional.Should().Equal(
            ["content:publish", "messaging.send", "messaging:send", "reviews.post"],
            "the provisional catalogue may only shrink. An entry added here is a permission or " +
            "gate attributed to nobody, which is how these three survived twelve phases");
    }

    /// <summary>The kernel's service ports — the abstractions a module is invited to depend on.</summary>
    private static IReadOnlyList<Type> KernelPorts() =>
        [.. typeof(IPlatformModule).Assembly
            .GetTypes()
            .Where(type =>
                type.IsInterface &&
                type.IsPublic &&
                type.Namespace == "Diaspora.Platform.Services.Abstractions")];

    /// <summary>
    /// <c>IEnumerable&lt;IPort&gt;</c> is how the platform asks for a port that may not be there —
    /// Trust's document links have taken one that way since B3 — so the dependency is the element
    /// type rather than the collection.
    /// </summary>
    private static Type Unwrap(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
            ? type.GetGenericArguments()[0]
            : type;

    /// <summary>
    /// Every type any endpoint binds with <c>[AsParameters]</c>, found by walking every method in
    /// every module assembly — compiler-generated lambda classes included, which is where minimal
    /// API route handlers actually live.
    /// </summary>
    private static IReadOnlyList<Type> AsParametersTypes()
    {
        const string Attribute = "Microsoft.AspNetCore.Http.AsParametersAttribute";

        var types = new HashSet<Type>();

        foreach (var assembly in ModuleAssemblyScanner.FindPlatformAssemblies())
        {
            IEnumerable<Type> candidates;

            try
            {
                candidates = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                candidates = exception.Types.Where(type => type is not null)!;
            }

            foreach (var type in candidates)
            {
                var methods = type.GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Instance | BindingFlags.Static |
                    BindingFlags.DeclaredOnly);

                foreach (var parameter in methods.SelectMany(method => method.GetParameters()))
                {
                    if (parameter.GetCustomAttributes()
                        .Any(attribute => attribute.GetType().FullName == Attribute))
                    {
                        types.Add(parameter.ParameterType);
                    }
                }
            }
        }

        return [.. types];
    }
}
