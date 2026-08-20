using Diaspora.Contracts.Authorization;

namespace Diaspora.Platform.Modules.Contributors;

/// <summary>
/// Implemented by a module to declare the permissions it owns.
/// <para>
/// This is the extension point that replaced B2's closed <c>PermissionResolver</c>. Before
/// it, adding Marketplace meant editing an array inside Identity — which meant Identity
/// knowing what a marketplace listing is, and every module reshaping the foundation on its
/// way in. Now Identity declares Identity's permissions, Marketplace declares
/// Marketplace's, and the kernel composes the catalogue at startup.
/// </para>
/// <para>
/// Implementations are discovered automatically. They must be stateless and safe to call
/// during startup, before the container is built.
/// </para>
/// </summary>
public interface IPermissionContributor
{
    /// <summary>
    /// The owning module's name. Must match the <see cref="IPlatformModule.Name"/> of the
    /// module that ships this contributor; the kernel uses it to attribute conflicts.
    /// </summary>
    string Module { get; }

    /// <summary>
    /// Every permission this module owns. Keys must be unique across the platform — a
    /// collision fails startup rather than letting one module silently redefine another's
    /// authority.
    /// </summary>
    IEnumerable<PermissionDefinition> GetPermissions();
}
