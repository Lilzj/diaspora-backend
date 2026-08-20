using Diaspora.Platform.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace Diaspora.Platform.Validation.Validators;

/// <summary>
/// Every contributor is registered once, resolves, and says which module it belongs to.
/// <para>
/// <b>The failure this exists for has already happened twice on this platform, in both
/// directions.</b> A reference-data seeder that was never registered took all 309 integration
/// tests down at host start, because nothing had said out loud that it needed registering. And a
/// contributor registered twice is worse than one registered never: the platform starts, the
/// catalogue composes, and every contribution is counted twice — which for a permission catalogue
/// is a duplicate-key exception and for an additive reference-data seam is silently doubled data.
/// </para>
/// <para>
/// <b>Resolution is the circular-dependency check.</b> The container detects a cycle when it tries
/// to build the graph and not before, so asking for every contributor here is what turns "this
/// would have thrown on the first request that needed it" into "this threw at boot, named, with
/// the rest of the report beside it".
/// </para>
/// </summary>
public sealed class ContributorRegistrationValidator : IStartupValidator
{
    private readonly ModuleRegistry _registry;

    public ContributorRegistrationValidator(ModuleRegistry registry) => _registry = registry;

    public string Name => nameof(ContributorRegistrationValidator);

    public IEnumerable<StartupFinding> Validate(StartupValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var findings = new List<StartupFinding>();

        var moduleNames = KnownModules.Names(_registry);

        foreach (var seam in ContributorSeams.Find(context.Assemblies))
        {
            var kernelConstructed = ContributorSeams.KernelConstructed.Contains(seam.Name);

            foreach (var implementation in ContributorSeams.Implementations(seam, context.Assemblies))
            {
                findings.AddRange(
                    Registrations(context, seam, implementation, kernelConstructed));
            }

            findings.AddRange(Resolvable(context, seam, moduleNames));
        }

        return findings;
    }

    private static IEnumerable<StartupFinding> Registrations(
        StartupValidationContext context, Type seam, Type implementation, bool kernelConstructed)
    {
        var count = context.CountRegistrations(seam, implementation);
        var subject = $"{implementation.Name} ({seam.Name})";

        if (count > 1)
        {
            yield return StartupFinding.Error(
                "SV2001",
                subject,
                $"is registered {count} times. Every seam here is additive, so each contribution " +
                "it declares is composed once per registration — a duplicate permission key at " +
                "best and silently doubled reference data at worst.");

            yield break;
        }

        if (count > 0)
        {
            yield break;
        }

        if (kernelConstructed)
        {
            yield return StartupFinding.Error(
                "SV2002",
                subject,
                "is not registered. The kernel scans for this seam, so a contributor it does not " +
                "see contributes nothing: its permissions are undeclared, its gates are missing, " +
                "and every policy that names them fails the boot or denies every request.");
        }
        else
        {
            yield return StartupFinding.Warning(
                "SV2003",
                subject,
                "implements a contributor seam and is not registered for it. The owning module " +
                "registers its own reference-data seams, so this is either deliberate or a " +
                "contributor whose declarations nothing will ever read.");
        }
    }

    private static IEnumerable<StartupFinding> Resolvable(
        StartupValidationContext context, Type seam, IReadOnlySet<string> moduleNames)
    {
        if (!context.IsRegistered(seam))
        {
            yield break;
        }

        IEnumerable<object?> resolved;
        string? resolutionFailure = null;

        try
        {
            // Materialised inside the try, because GetServices is lazy and a cycle would otherwise
            // surface at the foreach below — outside the handler that exists to catch it.
            resolved = [.. context.Services.GetServices(seam)];
        }
#pragma warning disable CA1031 // Any resolution failure is the finding, whatever its shape.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            resolved = [];
            resolutionFailure = exception.Message.Replace(
                Environment.NewLine, " ", StringComparison.Ordinal);
        }

        if (resolutionFailure is not null)
        {
            yield return StartupFinding.Error(
                "SV2004",
                seam.Name,
                "cannot be resolved from the container, so nothing that depends on it will work — " +
                "a missing dependency, or a cycle the container found while building the graph: " +
                resolutionFailure);

            yield break;
        }

        foreach (var contributor in resolved.OfType<object>())
        {
            var declared = ContributorSeams.DeclaredModule(contributor);

            if (declared is null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(declared))
            {
                yield return StartupFinding.Error(
                    "SV2005",
                    contributor.GetType().Name,
                    "declares an empty module name. The kernel attributes conflicts by it, so a " +
                    "collision involving this contributor would report which of two empty strings " +
                    "owns the key.");
            }
            else if (!KnownModules.IsRecognised(declared, moduleNames))
            {
                yield return StartupFinding.Warning(
                    "SV2006",
                    contributor.GetType().Name,
                    $"is attributed to module '{declared}', which no registered module is named. " +
                    "Conflict messages and the platform manifest will both name a module that " +
                    "does not exist.");
            }
        }
    }
}
