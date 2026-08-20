using Diaspora.Platform.Modules;

namespace Diaspora.Platform.Validation.Validators;

/// <summary>
/// Every module declares the metadata the platform attributes things by.
/// <para>
/// <b>A blank module name is not cosmetic.</b> Permissions, gates, policies, telemetry sources and
/// database schemas are all attributed to a module by that string, and the kernel's conflict
/// messages name it — so a module with no name produces conflict reports that say which of two
/// empty strings owns a permission, and a manifest with a hole in it.
/// </para>
/// <para>
/// Order is checked because it decides composition sequence, and a vertical sharing an order with
/// another gets whichever registration the scan happened to reach first. That is a warning rather
/// than an error: the platform has run correctly with equal orders where neither module registers
/// anything the other replaces, and refusing to start over it would be the rule overreaching.
/// </para>
/// </summary>
public sealed class ModuleMetadataValidator : IStartupValidator
{
    private readonly ModuleRegistry _registry;

    public ModuleMetadataValidator(ModuleRegistry registry) => _registry = registry;

    public string Name => nameof(ModuleMetadataValidator);

    public IEnumerable<StartupFinding> Validate(StartupValidationContext context)
    {
        var modules = _registry.Modules;

        foreach (var registration in modules)
        {
            var type = registration.Module.GetType().FullName ?? registration.Module.GetType().Name;

            if (string.IsNullOrWhiteSpace(registration.Name))
            {
                yield return StartupFinding.Error(
                    "SV1001",
                    type,
                    "declares no module name. Permissions, gates, policies and schemas are all " +
                    "attributed by it, and the platform manifest reports it.");
            }

            if (string.IsNullOrWhiteSpace(registration.Version))
            {
                yield return StartupFinding.Error(
                    "SV1002",
                    registration.Name,
                    "declares no version. The manifest and the readiness report both publish it.");
            }
            else if (!Version.TryParse(registration.Version, out _))
            {
                yield return StartupFinding.Warning(
                    "SV1003",
                    registration.Name,
                    $"declares version '{registration.Version}', which is not a parseable version. " +
                    "The platform compares these between releases.");
            }
        }

        foreach (var collision in modules
            .GroupBy(registration => registration.Module.Order)
            .Where(group => group.Count() > 1))
        {
            yield return StartupFinding.Warning(
                "SV1004",
                string.Join(", ", collision.Select(registration => registration.Name)),
                $"share composition order {collision.Key}. Whichever the assembly scan reaches " +
                "first configures services first, and a module that replaces a default registered " +
                "by the other would win or lose by accident.");
        }
    }
}
