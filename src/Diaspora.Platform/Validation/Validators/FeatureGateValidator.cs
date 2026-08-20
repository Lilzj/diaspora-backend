using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Authorization;
using Diaspora.Platform.Modules;

namespace Diaspora.Platform.Validation.Validators;

/// <summary>
/// Feature gates: unique, described, namespaced, reachable — and staff-only when the capability
/// behind them is.
/// <para>
/// <b>The staff check is the one the brief asks to fail a boot over, and it is answerable because
/// the platform already holds both halves.</b> A gate says whether a client should render a
/// capability; the permission behind it says who may actually use it. When the permission is
/// role-scoped to staff and the gate is not <c>StaffOnly</c>, the two disagree — and the disagreement
/// is visible: every member sees an unlocked control, uses it, and collects a 403 from a server that
/// was right all along. Nothing is exposed, which is why it survives a security review, and the
/// product is wrong on every screen that renders the gate.
/// </para>
/// <para>
/// <b>Matched by exact name, and skipped when there is no match.</b> A gate is checked against the
/// permission whose key is its dotted name with colons — <c>guides.moderation.manage</c> against
/// <c>guides:moderation:manage</c>. A gate that protects something with no single permission behind
/// it is not guessed at: a rule that inferred a relationship would report the modules whose naming
/// is most careful and miss the ones whose naming is loosest, which is precisely backwards.
/// </para>
/// </summary>
public sealed class FeatureGateValidator : IStartupValidator
{
    private readonly IFeatureCatalog _features;
    private readonly IPermissionCatalog _permissions;
    private readonly ModuleRegistry _registry;

    public FeatureGateValidator(
        IFeatureCatalog features, IPermissionCatalog permissions, ModuleRegistry registry)
    {
        _features = features;
        _permissions = permissions;
        _registry = registry;
    }

    public string Name => nameof(FeatureGateValidator);

    public IEnumerable<StartupFinding> Validate(StartupValidationContext context)
    {
        var moduleNames = KnownModules.Names(_registry);

        foreach (var duplicate in _features.All
            .GroupBy(feature => feature.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1))
        {
            yield return StartupFinding.Error(
                "SV4001",
                duplicate.Key,
                "is declared by more than one module. A client switching on it would get whichever " +
                "declaration composed last, and the two modules disagree about what it means.");
        }

        foreach (var feature in _features.All)
        {
            foreach (var finding in Validate(feature, moduleNames))
            {
                yield return finding;
            }
        }
    }

    private IEnumerable<StartupFinding> Validate(
        FeatureDefinition feature, IReadOnlySet<string> moduleNames)
    {
        if (string.IsNullOrWhiteSpace(feature.Description))
        {
            yield return StartupFinding.Warning(
                "SV4002",
                feature.Name,
                "has no description. A blocked gate is rendered to a member with a reason, and an " +
                "undescribed gate is one nobody can write that reason for.");
        }

        if (!KnownModules.IsRecognised(feature.Module, moduleNames))
        {
            yield return StartupFinding.Warning(
                "SV4003",
                feature.Name,
                $"is attributed to module '{feature.Module}', which no registered module is named.");
        }

        // A ceiling below the floor is a gate that is closed at every tier — a capability the
        // platform believes it offers and no member can ever reach.
        if (feature.MaximumTier is { } ceiling && ceiling < feature.MinimumTier)
        {
            yield return StartupFinding.Error(
                "SV4004",
                feature.Name,
                $"opens at {feature.MinimumTier} and closes at {ceiling}, so it is shut at every " +
                "tier. Nothing can ever open it and no client will ever render it.");
        }

        var behind = _permissions.Find(feature.Name.Replace('.', ':'));

        if (behind is null)
        {
            yield break;
        }

        var permissionIsStaffOnly = AuthorizationCatalogValidator.IsStaffOnly(behind);

        if (permissionIsStaffOnly && !feature.StaffOnly)
        {
            yield return StartupFinding.Error(
                "SV4005",
                feature.Name,
                $"is open to members, but the permission behind it ('{behind.Key}') is restricted " +
                $"to {string.Join(", ", behind.Roles.OrderBy(role => role, StringComparer.Ordinal))}. " +
                "Every member would see the capability, use it, and be refused by the server. " +
                "Declare StaffOnly = true on the gate.");
        }
        else if (!permissionIsStaffOnly && feature.StaffOnly && behind.Roles.Count == 0)
        {
            yield return StartupFinding.Warning(
                "SV4006",
                feature.Name,
                $"is staff-only, but the permission behind it ('{behind.Key}') is granted by tier " +
                "to every role. The gate hides a capability the server would allow, which makes " +
                "the gate the only thing enforcing it.");
        }
    }
}
