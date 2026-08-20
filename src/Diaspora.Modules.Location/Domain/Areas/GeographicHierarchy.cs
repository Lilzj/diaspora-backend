using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Location.Domain.Areas;

/// <summary>
/// Reasoning about a set of geographic nodes that no single node can do alone.
/// <para>
/// A pure domain service: it takes nodes and returns answers, touches no repository and no
/// clock. That is what lets every rule in it be unit-tested against a handful of in-memory
/// areas rather than against a seeded database — and the rules are the ones most likely to be
/// subtly wrong, because they are the ones about ordering and containment.
/// </para>
/// </summary>
public static class GeographicHierarchy
{
    /// <summary>
    /// Orders a set of nodes into a breadcrumb, root first.
    /// <para>
    /// Ordered by path rather than by level: levels may be skipped, so a set containing a
    /// country, a city and a neighbourhood must still come back in that order without the
    /// caller having to know that no state exists between them.
    /// </para>
    /// </summary>
    public static IReadOnlyList<AdministrativeArea> ToBreadcrumb(
        IEnumerable<AdministrativeArea> areas)
    {
        ArgumentNullException.ThrowIfNull(areas);

        return areas
            .OrderBy(area => area.Path.Depth)
            .ThenBy(area => area.Path.Value, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// The direct children of <paramref name="parent"/> within a candidate set.
    /// <para>
    /// "Direct" is a depth comparison, not a level one. A city whose country has no states
    /// is a direct child of that country even though their levels are two rungs apart.
    /// </para>
    /// </summary>
    public static IReadOnlyList<AdministrativeArea> DirectChildren(
        AdministrativeArea parent, IEnumerable<AdministrativeArea> candidates)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(candidates);

        return candidates
            .Where(candidate => candidate.ParentId == parent.Id)
            .OrderByDescending(candidate => candidate.Prominence)
            .ThenBy(candidate => candidate.Name.Value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Every node beneath <paramref name="ancestor"/>, at any depth.</summary>
    public static IReadOnlyList<AdministrativeArea> Descendants(
        AdministrativeArea ancestor, IEnumerable<AdministrativeArea> candidates)
    {
        ArgumentNullException.ThrowIfNull(ancestor);
        ArgumentNullException.ThrowIfNull(candidates);

        return candidates.Where(candidate => candidate.IsWithin(ancestor)).ToList();
    }

    /// <summary>
    /// The nearest common ancestor path of two nodes, or null when they share only the root.
    /// <para>
    /// Answers "how close are these two places administratively?" without a distance
    /// calculation — useful for ranking a result in the same neighbourhood above one merely
    /// in the same city, which a raw kilometre figure does not capture.
    /// </para>
    /// </summary>
    public static HierarchyPath? CommonAncestor(AdministrativeArea first, AdministrativeArea second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var left = first.Path.Segments;
        var right = second.Path.Segments;
        var shared = new List<string>();

        for (var index = 0; index < Math.Min(left.Count, right.Count); index++)
        {
            if (!string.Equals(left[index], right[index], StringComparison.Ordinal))
            {
                break;
            }

            shared.Add(left[index]);
        }

        if (shared.Count == 0)
        {
            return null;
        }

        var path = HierarchyPath.ForRoot(shared[0]);

        for (var index = 1; index < shared.Count; index++)
        {
            var appended = path.Append(shared[index]);

            if (appended.IsFailure)
            {
                return path;
            }

            path = appended.Value;
        }

        return path;
    }

    /// <summary>
    /// The node at <paramref name="level"/> on the path from the root to
    /// <paramref name="area"/>, if the set contains it.
    /// <para>
    /// This is how "which city is this neighbourhood in?" is answered without a parent walk:
    /// every ancestor is already identified by the path, so the question is a filter rather
    /// than a traversal.
    /// </para>
    /// </summary>
    public static AdministrativeArea? AncestorAtLevel(
        AdministrativeArea area, AdministrativeLevel level, IEnumerable<AdministrativeArea> candidates)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(candidates);

        return candidates.FirstOrDefault(
            candidate => candidate.Level == level && candidate.Path.Contains(area.Path));
    }

    /// <summary>
    /// The identifiers a relocation must rewrite: the moved node's descendants.
    /// </summary>
    public static IReadOnlyList<AreaId> DescendantIdsOf(
        HierarchyPath path, IEnumerable<AdministrativeArea> candidates)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(candidates);

        return candidates
            .Where(candidate => candidate.Path.Value.StartsWith(
                path.DescendantPrefix, StringComparison.Ordinal))
            .Select(candidate => candidate.Id)
            .ToList();
    }
}
