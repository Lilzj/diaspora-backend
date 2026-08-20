using Diaspora.Contracts.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Diaspora.Platform.Authorization.AspNetCore;

/// <summary>Requires the caller to hold a named permission.</summary>
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        Permission = permission;
    }

    public string Permission { get; }
}

/// <summary>Requires the caller to have reached a tier on the platform access ladder.</summary>
public sealed class AccessTierRequirement : IAuthorizationRequirement
{
    public AccessTierRequirement(AccessTier minimumTier, bool requiresExactTier = false)
    {
        MinimumTier = minimumTier;
        RequiresExactTier = requiresExactTier;
    }

    public AccessTier MinimumTier { get; }

    /// <summary>
    /// When set, the caller must sit at exactly <see cref="MinimumTier"/>. Used for the
    /// handful of capabilities that stop applying as a member progresses.
    /// </summary>
    public bool RequiresExactTier { get; }
}
