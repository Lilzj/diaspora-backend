using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Authorization;
using Diaspora.Platform.Modules;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// The whole platform's authorisation catalogues, composed once, from every module's contributor.
/// <para>
/// <b>Discovered rather than listed, for the reason <see cref="PlatformModel"/> records.</b> A
/// rule written against a hard-coded set of contributors stops applying the moment a module lands
/// without anybody remembering to add it — and a rule about naming conventions that silently
/// exempts the newest module is worse than none, because the report it produces reads as coverage.
/// </para>
/// <para>
/// Composing rather than reflecting also means these tests see exactly what the host sees: the
/// catalogues run their own validation in the constructor, so a suite that composes them is
/// already asserting the platform boots.
/// </para>
/// </summary>
internal static class AuthorizationModel
{
    private static readonly Lazy<PermissionCatalog> LazyPermissions = new(() =>
        new PermissionCatalog(Contributors<IPermissionContributor>()));

    private static readonly Lazy<PolicyCatalog> LazyPolicies = new(() =>
        new PolicyCatalog(Contributors<IPolicyContributor>(), LazyPermissions.Value));

    private static readonly Lazy<FeatureCatalog> LazyFeatures = new(() =>
        new FeatureCatalog(Contributors<IFeatureContributor>()));

    public static IReadOnlyList<PermissionDefinition> Permissions => LazyPermissions.Value.All;

    public static IReadOnlyList<PolicyDefinition> Policies => LazyPolicies.Value.All;

    public static IReadOnlyList<FeatureDefinition> Features => LazyFeatures.Value.All;

    /// <summary>Every legacy policy name still answered, paired with the policy answering it.</summary>
    public static IReadOnlyList<(string Alias, PolicyDefinition Policy)> PolicyAliases =>
        [.. Policies.SelectMany(policy => policy.Aliases.Select(alias => (alias, policy)))];

    public static PolicyDefinition? FindPolicy(string name) => LazyPolicies.Value.Find(name);

    private static IReadOnlyList<TContributor> Contributors<TContributor>()
        where TContributor : class =>
        ModuleAssemblyScanner.CreateAll<TContributor>(
            ModuleAssemblyScanner.FindPlatformAssemblies());
}
