namespace Diaspora.Contracts.Authorization;

/// <summary>
/// A named authorisation policy, declared by the module whose endpoints reference it.
/// <para>
/// A policy is the name an endpoint uses; a permission is the thing being checked. Keeping
/// them separate means an endpoint's attribute never has to change when the rule behind it
/// does, and it is why the platform can host both permission-backed and tier-backed gates
/// under one <c>[RequirePolicy(...)]</c> vocabulary.
/// </para>
/// <para>
/// Exactly one of <see cref="Permission"/> or <see cref="MinimumTier"/> must be set.
/// </para>
/// </summary>
/// <param name="Name">
/// The policy name endpoints reference. Convention is <c>module.resource.action</c> —
/// dotted and lower case, to stay visibly distinct from the colon-separated permission it
/// usually maps to. See <see cref="Aliases"/> for renaming one without a flag day.
/// </param>
/// <param name="Module">The owning module.</param>
public sealed record PolicyDefinition(string Name, string Module)
{
    /// <summary>The permission the caller must hold.</summary>
    public string? Permission { get; init; }

    /// <summary>The tier the caller must have reached.</summary>
    public AccessTier? MinimumTier { get; init; }

    /// <summary>
    /// When set, the caller must sit at exactly this tier rather than at or above it.
    /// </summary>
    public bool RequiresExactTier { get; init; }

    /// <summary>
    /// Former names this policy still answers to, registered with ASP.NET Core alongside
    /// <see cref="Name"/> and carrying byte-for-byte the same requirements.
    /// <para>
    /// <b>The point is that renaming a policy stops being a flag day.</b> A policy name is
    /// referenced from route attributes, from <c>RequireAuthorization</c> calls and from
    /// whatever a module has not been read for yet; renaming the constant leaves every one of
    /// those compiling against the new value, but a name reached any other way — configuration,
    /// a test fixture, an endpoint in a branch that has not merged — would resolve to nothing.
    /// An unregistered policy does not throw. ASP.NET Core denies the request, and the log says
    /// nothing about why. Declaring the old name as an alias makes the rename inert, and the
    /// alias is then deleted on its own schedule.
    /// </para>
    /// <para>
    /// Aliases are deprecated by construction: nothing new should reference one, and the
    /// catalogue refuses a set that collides with a canonical name or with another alias.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> Aliases { get; init; } = [];

    public static PolicyDefinition ForPermission(string name, string module, string permission) =>
        new(name, module) { Permission = permission };

    public static PolicyDefinition ForTier(string name, string module, AccessTier minimumTier) =>
        new(name, module) { MinimumTier = minimumTier };

    /// <summary>
    /// The same policy, also reachable under <paramref name="aliases"/>. Used by a module that
    /// has renamed a policy and is keeping the old name alive for a release.
    /// </summary>
    public PolicyDefinition WithAliases(params string[] aliases)
    {
        ArgumentNullException.ThrowIfNull(aliases);

        return this with { Aliases = [.. Aliases, .. aliases] };
    }
}
