using Diaspora.Contracts.Authorization;

namespace Diaspora.Platform.Authorization;

/// <summary>
/// The composed, platform-wide permission catalogue.
/// <para>
/// Built once at startup from every <see cref="Modules.Contributors.IPermissionContributor"/>
/// the host discovers, then treated as immutable. Resolution is a pure function of the
/// subject, so it costs no I/O and can be called on the token-issuance path.
/// </para>
/// </summary>
public interface IPermissionCatalog
{
    /// <summary>Every declared permission, ordered by module then key.</summary>
    IReadOnlyList<PermissionDefinition> All { get; }

    /// <summary>The permissions a subject holds.</summary>
    IReadOnlyList<string> Resolve(AuthorizationSubject subject);

    /// <summary>Whether a subject holds one named permission.</summary>
    bool IsGranted(string permission, AuthorizationSubject subject);

    /// <summary>Looks up a declaration by key; <c>null</c> when nothing declares it.</summary>
    PermissionDefinition? Find(string permission);
}
