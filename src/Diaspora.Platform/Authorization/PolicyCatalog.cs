using System.Collections.Frozen;
using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Platform.Authorization;

/// <summary>
/// Composes every module's policy declarations, and validates them against the permission
/// catalogue.
/// <para>
/// The validation is the valuable part. A policy naming a permission nothing declares is
/// a typo that turns into a permanently-denied endpoint — the kind of bug that reaches
/// production because the failing path is the safe one and nobody tests it. Failing the
/// boot instead makes it a thirty-second fix.
/// </para>
/// <para>
/// <b>Aliases are held in the same namespace as canonical names, deliberately.</b> A legacy
/// name that collides with a live policy would silently give one module's endpoints another
/// module's requirements, so the composition refuses it exactly as it refuses a duplicate
/// name. There is one name space for policies and an alias is a name.
/// </para>
/// </summary>
public sealed class PolicyCatalog : IPolicyCatalog
{
    private readonly FrozenDictionary<string, PolicyDefinition> _byName;

    public PolicyCatalog(IEnumerable<IPolicyContributor> contributors, IPermissionCatalog permissions)
    {
        ArgumentNullException.ThrowIfNull(contributors);
        ArgumentNullException.ThrowIfNull(permissions);

        var declarations = contributors
            .SelectMany(contributor => contributor
                .GetPolicies()
                .Select(definition => (Contributor: contributor.Module, Definition: definition)))
            .ToList();

        var problems = new List<string>();

        problems.AddRange(declarations
            .GroupBy(d => d.Definition.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group =>
                $"policy '{group.Key}' is declared by {string.Join(", ", group.Select(d => d.Contributor).Distinct())}"));

        foreach (var (contributor, definition) in declarations)
        {
            if (definition.Permission is null && definition.MinimumTier is null)
            {
                problems.Add(
                    $"policy '{definition.Name}' from {contributor} sets neither a permission nor a tier, " +
                    "so it would authorise every authenticated caller");
                continue;
            }

            if (definition.Permission is { } permission && permissions.Find(permission) is null)
            {
                problems.Add(
                    $"policy '{definition.Name}' from {contributor} requires permission '{permission}', " +
                    "which no module declares");
            }
        }

        var canonical = declarations
            .Select(d => d.Definition.Name)
            .ToHashSet(StringComparer.Ordinal);

        var aliases = new Dictionary<string, PolicyDefinition>(StringComparer.Ordinal);

        foreach (var (contributor, definition) in declarations)
        {
            foreach (var alias in definition.Aliases)
            {
                if (string.IsNullOrWhiteSpace(alias))
                {
                    problems.Add($"policy '{definition.Name}' from {contributor} declares a blank alias");
                }
                else if (canonical.Contains(alias))
                {
                    problems.Add(
                        $"policy '{definition.Name}' from {contributor} declares alias '{alias}', " +
                        "which is already a policy in its own right");
                }
                else if (!aliases.TryAdd(alias, definition))
                {
                    problems.Add(
                        $"alias '{alias}' is declared by both '{aliases[alias].Name}' and " +
                        $"'{definition.Name}'");
                }
            }
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                $"The authorisation policy catalogue is inconsistent: {string.Join("; ", problems)}.");
        }

        All = declarations
            .Select(d => d.Definition)
            .OrderBy(d => d.Module, StringComparer.Ordinal)
            .ThenBy(d => d.Name, StringComparer.Ordinal)
            .ToArray();

        // One lookup table over both kinds of name. The validation above has already established
        // that the two sets are disjoint; building the dictionary this way means that if it ever
        // stops being true, ToFrozenDictionary throws on the duplicate key rather than one name
        // quietly winning.
        _byName = All
            .Select(definition => (Name: definition.Name, Definition: definition))
            .Concat(aliases.Select(entry => (Name: entry.Key, Definition: entry.Value)))
            .ToFrozenDictionary(entry => entry.Name, entry => entry.Definition, StringComparer.Ordinal);
    }

    public IReadOnlyList<PolicyDefinition> All { get; }

    /// <summary>
    /// Resolves a canonical name or any declared alias to the one definition behind it. An alias
    /// is never a separate entry in <see cref="All"/>: the catalogue has as many policies as the
    /// modules declared, and some of them answer to more than one name.
    /// </summary>
    public PolicyDefinition? Find(string policyName) =>
        string.IsNullOrWhiteSpace(policyName)
            ? null
            : _byName.GetValueOrDefault(policyName);
}
