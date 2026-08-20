using Diaspora.Contracts.Authorization;
using Diaspora.Contracts.Enums;
using Diaspora.Platform.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Diaspora.Platform.Validation.Validators;

/// <summary>
/// The permission and policy catalogues, checked against each other and against what ASP.NET Core
/// actually registered.
/// <para>
/// <b>The catalogues already refuse to compose when they are inconsistent, and this is the check
/// that starts where those stop.</b> <c>PermissionCatalog</c> throws on a duplicate key and
/// <c>PolicyCatalog</c> throws on a policy naming a permission nobody declares — both at
/// construction, both loud. What neither can see is the step afterwards: whether
/// <c>AddPlatformAuthorizationPolicies</c> was ever called, and therefore whether the policy names
/// every endpoint attribute carries were turned into registered policies at all.
/// </para>
/// <para>
/// <b>That gap is the dangerous one because its failure mode is silence.</b> An unregistered policy
/// does not throw when an endpoint references it — ASP.NET Core denies the request. Every protected
/// endpoint on the platform would answer 403 to a correctly authorised caller, on a host whose logs
/// say nothing at all.
/// </para>
/// </summary>
public sealed class AuthorizationCatalogValidator : IStartupValidator
{
    /// <summary>
    /// Derived from the role enum rather than listed, so a seventh role added above Moderator is
    /// staff on the day it is added rather than on the day somebody remembers this file.
    /// </summary>
    private static readonly HashSet<string> StaffRoles = new(
        Enum.GetValues<UserRole>()
            .Where(role => role >= UserRole.Moderator)
            .Select(role => role.ToString()),
        StringComparer.OrdinalIgnoreCase);

    private readonly IPermissionCatalog _permissions;
    private readonly IPolicyCatalog _policies;
    private readonly IAuthorizationPolicyProvider _provider;

    public AuthorizationCatalogValidator(
        IPermissionCatalog permissions,
        IPolicyCatalog policies,
        IAuthorizationPolicyProvider provider)
    {
        _permissions = permissions;
        _policies = policies;
        _provider = provider;
    }

    public string Name => nameof(AuthorizationCatalogValidator);

    public IEnumerable<StartupFinding> Validate(StartupValidationContext context)
    {
        foreach (var finding in ValidatePermissions())
        {
            yield return finding;
        }

        foreach (var finding in ValidatePolicies())
        {
            yield return finding;
        }
    }

    private IEnumerable<StartupFinding> ValidatePermissions()
    {
        foreach (var duplicate in _permissions.All
            .GroupBy(permission => permission.Key, StringComparer.Ordinal)
            .Where(group => group.Count() > 1))
        {
            yield return StartupFinding.Error(
                "SV3001",
                duplicate.Key,
                "is declared more than once. Two modules believing they control one capability is " +
                "a security question, not a merge conflict.");
        }

        foreach (var permission in _permissions.All)
        {
            if (string.IsNullOrWhiteSpace(permission.Description))
            {
                yield return StartupFinding.Warning(
                    "SV3002",
                    permission.Key,
                    "has no description. The manifest publishes the catalogue, and an undescribed " +
                    "permission is one nobody outside its author can answer 'who can do this?' for.");
            }

            // Unreachable authority: a member-facing permission that no tier grants and no role
            // names. It reads like a capability the platform offers and nothing can ever hold it.
            //
            // The role clause is load-bearing. A permission with no minimum tier but an explicit
            // role set is the platform's documented way of saying "held by administrators and by
            // nobody else, however far a member progresses" — the outbox operator surface is
            // exactly that. It never reaches a token's permission snapshot, and the authorisation
            // handler's catalogue fallback grants it anyway. Reporting it would flag the two
            // permissions on the platform that are most deliberately shaped.
            if (permission is { Audience: PermissionAudience.Member, MinimumTier: null } &&
                permission.Roles.Count == 0)
            {
                yield return StartupFinding.Warning(
                    "SV3003",
                    permission.Key,
                    "is a member permission that no tier grants and no role names, so no subject " +
                    "can ever hold it. Give it a minimum tier, name the roles that hold it, or " +
                    "declare it as a service permission.");
            }
        }
    }

    private IEnumerable<StartupFinding> ValidatePolicies()
    {
        foreach (var duplicate in _policies.All
            .GroupBy(policy => policy.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1))
        {
            yield return StartupFinding.Error(
                "SV3004",
                duplicate.Key,
                "is declared by more than one module. An endpoint naming it would get whichever " +
                "registration composed last.");
        }

        foreach (var policy in _policies.All)
        {
            if (policy.Permission is { } permission && _permissions.Find(permission) is null)
            {
                yield return StartupFinding.Error(
                    "SV3005",
                    policy.Name,
                    $"requires permission '{permission}', which no module declares. Every endpoint " +
                    "behind this policy denies every caller, and nothing logs why.");

                continue;
            }

            // The canonical name and every alias. An alias exists to keep a rename from breaking a
            // caller, so an alias that was declared and never registered is the same silent 403 as
            // an unregistered policy — and worse, because the rename it was covering has shipped.
            foreach (var name in Names(policy))
            {
                if (Registration(name) is { } finding)
                {
                    yield return finding;
                }
            }
        }
    }

    private static IEnumerable<string> Names(PolicyDefinition policy) =>
        [policy.Name, .. policy.Aliases];

    private StartupFinding? Registration(string name)
    {
        AuthorizationPolicy? registered;

        try
        {
            registered = _provider.GetPolicyAsync(name).GetAwaiter().GetResult();
        }
#pragma warning disable CA1031 // A provider that throws is exactly as broken as one that returns null.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            return StartupFinding.Error(
                "SV3006",
                name,
                "could not be read back from the authorisation policy provider: " +
                exception.Message.Replace(Environment.NewLine, " ", StringComparison.Ordinal));
        }

        return registered is null
            ? StartupFinding.Error(
                "SV3006",
                name,
                "is declared by a module but was never registered with ASP.NET Core. Endpoints " +
                "naming it deny every caller and answer 403 to correctly authorised requests, " +
                "with nothing in the log to say so. AddPlatformAuthorizationPolicies turns " +
                "declarations into registrations.")
            : null;
    }

    /// <summary>
    /// Whether a permission is staff-only: it names roles, and every role it names is staff.
    /// Exposed so the feature-gate validator can ask the same question of the permission behind a
    /// gate without the two disagreeing about what "staff" means.
    /// </summary>
    internal static bool IsStaffOnly(PermissionDefinition permission)
    {
        ArgumentNullException.ThrowIfNull(permission);

        return permission.Roles.Count > 0 && permission.Roles.All(StaffRoles.Contains);
    }
}
