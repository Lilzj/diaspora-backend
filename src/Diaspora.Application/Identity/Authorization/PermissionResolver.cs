using Diaspora.Application.Identity.Abstractions;
using Diaspora.Domain.Identity.AccessStates;
using Diaspora.Domain.Identity.Users;
using Diaspora.Platform.Authorization;

namespace Diaspora.Application.Identity.Authorization;

/// <summary>
/// Resolves the effective permission set for a role at an access state.
/// <para>
/// The rules themselves no longer live here. B2's version held four private arrays and a
/// chain of role comparisons, which meant Marketplace, Jobs and Messaging permissions were
/// declared inside Identity and every new module edited this file. It is now an adapter:
/// it projects Identity's access state onto a platform tier and asks the catalogue, which
/// every module contributes to.
/// </para>
/// <para>
/// The signature is unchanged on purpose. Handlers, token issuance and the tests written
/// against B2 all still compile — the extensibility work is behind the interface, not in
/// front of it.
/// </para>
/// </summary>
public sealed class PermissionResolver : IPermissionResolver
{
    private readonly IPermissionCatalog _catalog;

    public PermissionResolver(IPermissionCatalog catalog)
    {
        _catalog = catalog;
    }

    public IReadOnlyList<string> Resolve(UserRole role, AccessState accessState)
    {
        ArgumentNullException.ThrowIfNull(role);
        ArgumentNullException.ThrowIfNull(accessState);

        return _catalog.Resolve(AccessTierMap.ToSubject(accessState, role));
    }
}
