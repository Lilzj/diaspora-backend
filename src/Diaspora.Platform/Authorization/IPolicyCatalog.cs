using Diaspora.Contracts.Authorization;

namespace Diaspora.Platform.Authorization;

/// <summary>
/// The composed, platform-wide authorisation policy catalogue.
/// <para>
/// The host walks this at startup and registers one ASP.NET Core policy per entry, which
/// is why adding a module never means editing <c>AddAuthorization</c>.
/// </para>
/// </summary>
public interface IPolicyCatalog
{
    /// <summary>
    /// Every declared policy, ordered by module then name. One entry per policy — a policy that
    /// carries <see cref="PolicyDefinition.Aliases"/> appears once, under its canonical name.
    /// </summary>
    IReadOnlyList<PolicyDefinition> All { get; }

    /// <summary>
    /// Looks up a policy by its canonical name or by any alias it declares; <c>null</c> when
    /// nothing declares it.
    /// </summary>
    PolicyDefinition? Find(string policyName);
}
