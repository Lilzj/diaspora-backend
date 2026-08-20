using System.Collections.Frozen;
using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Platform.Authorization;

/// <summary>
/// Composes every module's permission declarations into one catalogue.
/// <para>
/// Duplicate keys are a startup failure, not a last-one-wins merge. Two modules declaring
/// the same permission means one of them believes it controls something it does not, and
/// the difference between those two beliefs is a security bug that would only surface when
/// somebody exercised the wrong path.
/// </para>
/// </summary>
public sealed class PermissionCatalog : IPermissionCatalog
{
    private readonly FrozenDictionary<string, PermissionDefinition> _byKey;
    private readonly PermissionDefinition[] _memberGrantable;

    public PermissionCatalog(IEnumerable<IPermissionContributor> contributors)
    {
        ArgumentNullException.ThrowIfNull(contributors);

        var declarations = contributors
            .SelectMany(contributor => contributor
                .GetPermissions()
                .Select(definition => (Contributor: contributor.Module, Definition: definition)))
            .ToList();

        var duplicates = declarations
            .GroupBy(d => d.Definition.Key, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group =>
                $"'{group.Key}' declared by {string.Join(", ", group.Select(d => d.Contributor).Distinct())}")
            .ToList();

        if (duplicates.Count > 0)
        {
            throw new InvalidOperationException(
                "Two modules declared the same permission, so the platform cannot tell which one " +
                $"owns it: {string.Join("; ", duplicates)}.");
        }

        All = declarations
            .Select(d => d.Definition)
            .OrderBy(d => d.Module, StringComparer.Ordinal)
            .ThenBy(d => d.Key, StringComparer.Ordinal)
            .ToArray();

        _byKey = All.ToFrozenDictionary(d => d.Key, StringComparer.Ordinal);

        // Service permissions are filtered out once, here, rather than on every resolve:
        // no subject can ever hold one, so they do not belong in the loop.
        _memberGrantable = All
            .Where(d => d.Audience == PermissionAudience.Member && d.MinimumTier is not null)
            .ToArray();
    }

    public IReadOnlyList<PermissionDefinition> All { get; }

    public PermissionDefinition? Find(string permission) =>
        string.IsNullOrWhiteSpace(permission)
            ? null
            : _byKey.GetValueOrDefault(permission);

    public IReadOnlyList<string> Resolve(AuthorizationSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        if (subject.Tier == AccessTier.Anonymous)
        {
            return [];
        }

        // A restricted account keeps its identity and loses its capabilities, whatever its
        // role. A suspended administrator is a suspended member.
        if (subject.IsRestricted)
        {
            return _memberGrantable
                .Where(d => d.GrantedWhenRestricted)
                .Select(d => d.Key)
                .ToArray();
        }

        var granted = new List<string>(_memberGrantable.Length);

        foreach (var definition in _memberGrantable)
        {
            if (Grants(definition, subject))
            {
                granted.Add(definition.Key);
            }
        }

        return granted;
    }

    public bool IsGranted(string permission, AuthorizationSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        var definition = Find(permission);
        if (definition is null ||
            definition.Audience != PermissionAudience.Member ||
            subject.Tier == AccessTier.Anonymous)
        {
            return false;
        }

        return subject.IsRestricted
            ? definition.GrantedWhenRestricted
            : Grants(definition, subject);
    }

    private static bool Grants(PermissionDefinition definition, AuthorizationSubject subject)
    {
        if (subject.Tier < definition.MinimumTier)
        {
            return false;
        }

        if (definition.MaximumTier is { } ceiling && subject.Tier > ceiling)
        {
            return false;
        }

        return definition.Roles.Count == 0 || definition.Roles.Contains(subject.RoleName);
    }
}
