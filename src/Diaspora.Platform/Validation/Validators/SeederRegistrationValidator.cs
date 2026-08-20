using System.Reflection;
using Diaspora.Platform.Modules;
using Diaspora.Platform.Modules.Seeding;
using Microsoft.Extensions.Hosting;

namespace Diaspora.Platform.Validation.Validators;

/// <summary>
/// Every reference-data seeder in the platform is discovered, registered exactly once, uniquely
/// named, and attributed to a module that exists.
/// <para>
/// <b>This validator exists because the failure has already happened and cost a whole suite.</b>
/// A module shipped a seeder that nothing registered: the host started, the catalogue stayed empty,
/// and 309 integration tests died at host start with an error that named neither the seeder nor the
/// module. Nothing in the type system says "this class must be registered", and nothing ever will —
/// so something has to look.
/// </para>
/// <para>
/// <b>Found by interface since PSR-1, and the change closed a hole this rule had all along.</b> It
/// used to look for a <c>BackgroundService</c> named <c>…Seeder</c>, which is what the platform's
/// seeders happened to look like — and Location's <c>LocationCatalogSeedRunner</c>, the first
/// reference-data seeder the platform ever shipped, is neither. It had never once been checked. A
/// marker interface finds a seeder because it <em>is</em> one; a name convention finds the ones
/// somebody remembered to name correctly.
/// </para>
/// <para>
/// The name-based sweep is kept beside the interface one, at <c>Warning</c>, so a seeder written
/// the old way is reported rather than silently exempt — which is the state PSR-1 found and the
/// state this rule now refuses to return to.
/// </para>
/// </summary>
public sealed class SeederRegistrationValidator : IStartupValidator
{
    public string Name => nameof(SeederRegistrationValidator);

    public IEnumerable<StartupFinding> Validate(StartupValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var discovered = ModuleAssemblyScanner
            .FindImplementations<IReferenceDataSeeder>(context.Assemblies);

        foreach (var finding in Registered(context, discovered))
        {
            yield return finding;
        }

        foreach (var finding in UniquelyNamed(context))
        {
            yield return finding;
        }

        foreach (var finding in AttributedToARealModule(context))
        {
            yield return finding;
        }

        foreach (var finding in Discoverable(context, discovered))
        {
            yield return finding;
        }
    }

    /// <summary>
    /// <b>SV5001 / SV5002.</b> A seeder nothing registered will never run, and a seeder with no
    /// public entry point cannot be re-run once a separately-migrated schema exists.
    /// </summary>
    private static IEnumerable<StartupFinding> Registered(
        StartupValidationContext context, IReadOnlyList<Type> discovered)
    {
        foreach (var seeder in discovered)
        {
            // AddReferenceDataSeeder registers the concrete type and forwards both faces to it.
            // The forwarded descriptors carry a factory rather than an implementation type, so the
            // self-registration is the one a validator can actually read.
            var registered =
                context.CountRegistrations(seeder, seeder) > 0 ||
                context.Registrations.Any(descriptor =>
                    (descriptor.ServiceType == typeof(IHostedService) ||
                     descriptor.ServiceType == typeof(IReferenceDataSeeder)) &&
                    descriptor.ImplementationType == seeder);

            if (!registered)
            {
                yield return StartupFinding.Error(
                    "SV5001",
                    seeder.Name,
                    "is a reference-data seeder that nothing registered. It will never run, its " +
                    "catalogue will stay empty, and whatever depends on that catalogue will fail " +
                    "at the first request rather than here. Register it with " +
                    "AddReferenceDataSeeder in the module's ConfigureServices.");

                continue;
            }

            if (seeder.GetMethod("SeedAsync", BindingFlags.Public | BindingFlags.Instance) is null)
            {
                yield return StartupFinding.Warning(
                    "SV5002",
                    seeder.Name,
                    "has no public SeedAsync. A host that applies migrations separately — which " +
                    "the integration suite does — has to be able to seed again once the schema " +
                    "exists, and cannot reach a seeder whose only entry point is ExecuteAsync.");
            }
        }
    }

    /// <summary>
    /// <b>SV5003.</b> Two seeders answering to one name make every log line about either of them
    /// ambiguous, and make the runner's ordering depend on which assembly was scanned first.
    /// </summary>
    private static IEnumerable<StartupFinding> UniquelyNamed(StartupValidationContext context)
    {
        var runner = context.Services.GetService(typeof(ReferenceDataSeedRunner))
            as ReferenceDataSeedRunner;

        if (runner is null)
        {
            yield break;
        }

        foreach (var duplicate in runner.DuplicateNames)
        {
            yield return StartupFinding.Error(
                "SV5003",
                duplicate,
                "is claimed by more than one reference-data seeder. A seeder's name is how a " +
                "duplicate is detected and how an operator finds the one that failed, so two " +
                "cannot share it. Override IReferenceDataSeeder.Name on one of them.");
        }
    }

    /// <summary>
    /// <b>SV5004.</b> A seeder attributed to a module nothing registered sorts last rather than in
    /// its own module's slot — recoverable, and worth saying, because the cause is usually a typo
    /// in a string nobody reads twice.
    /// </summary>
    private static IEnumerable<StartupFinding> AttributedToARealModule(
        StartupValidationContext context)
    {
        if (context.Services.GetService(typeof(ReferenceDataSeedRunner))
                is not ReferenceDataSeedRunner runner ||
            context.Services.GetService(typeof(ModuleRegistry)) is not ModuleRegistry registry)
        {
            yield break;
        }

        var modules = registry.Modules
            .Select(registration => registration.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var seeder in runner.Seeders.Where(seeder => !modules.Contains(seeder.Module)))
        {
            yield return StartupFinding.Warning(
                "SV5004",
                seeder.Name,
                $"names module '{seeder.Module}', which no registered module answers to. It will " +
                "still seed, but last rather than in its module's order — and a catalogue that " +
                "another module's seeder depends on would then run too late.");
        }
    }

    /// <summary>
    /// <b>SV5005.</b> The old shape, reported rather than tolerated in silence.
    /// <para>
    /// A hosted service whose name says it seeds but which does not implement
    /// <see cref="IReferenceDataSeeder"/> is invisible to discovery: the runner will not call it,
    /// and a host that applies migrations separately will not seed it. Its own
    /// <c>StartAsync</c> still runs, so nothing breaks on a fresh deployment — which is precisely
    /// why this is a warning and why the shape survived undetected for eight phases.
    /// </para>
    /// </summary>
    private static IEnumerable<StartupFinding> Discoverable(
        StartupValidationContext context, IReadOnlyList<Type> discovered)
    {
        foreach (var candidate in NamedLikeASeeder(context))
        {
            if (discovered.Contains(candidate))
            {
                continue;
            }

            yield return StartupFinding.Warning(
                "SV5005",
                candidate.Name,
                "looks like a reference-data seeder and does not implement IReferenceDataSeeder, " +
                "so nothing discovers it. It will still run itself at startup, but a host that " +
                "applies migrations from a separate job cannot seed it afterwards — which is how " +
                "an integration suite ends up with an empty catalogue and no error naming it.");
        }
    }

    /// <summary>
    /// Hosted services whose name says they seed. Kept from the pre-PSR-1 rule, now only to report
    /// the ones the interface does not cover.
    /// </summary>
    private static IEnumerable<Type> NamedLikeASeeder(StartupValidationContext context)
    {
        foreach (var assembly in context.Assemblies)
        {
            Type?[] types;

            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                types = exception.Types;
            }

            foreach (var type in types)
            {
                if (type is { IsAbstract: false, IsGenericTypeDefinition: false } &&
                    (type.Name.EndsWith("Seeder", StringComparison.Ordinal) ||
                     type.Name.EndsWith("SeedRunner", StringComparison.Ordinal)) &&
                    typeof(IHostedService).IsAssignableFrom(type))
                {
                    yield return type;
                }
            }
        }
    }
}
