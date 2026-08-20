using Diaspora.Modules.Recommendations.Domain.Enums;

namespace Diaspora.Modules.Recommendations.Extensibility.Ranking;

/// <summary>
/// Every registered strategy implementation, resolvable by key.
/// <para>
/// <b>The seam between the catalogue and the code, and the place a mistyped key is caught.</b> A
/// catalogue row names a key; this answers whether anything implements it. Without the registry, a
/// row saying <c>locaton</c> would silently contribute nothing to every page on a surface, and the
/// only symptom would be a ranking that felt slightly off for however long it took somebody to
/// notice.
/// </para>
/// <para>
/// Fails startup on a duplicate key. Two implementations claiming one key means one of them is
/// silently ignored, and which one depends on registration order — a defect that appears when
/// somebody reorders a service registration for unrelated reasons.
/// </para>
/// </summary>
public interface IRankingStrategyRegistry
{
    /// <summary>The implementation for a key, or null when nothing implements it.</summary>
    IRankingStrategy? Find(string key);

    /// <summary>Every registered implementation.</summary>
    IReadOnlyList<IRankingStrategy> All { get; }

    /// <summary>Whether anything implements a key. What the catalogue validator asks.</summary>
    bool IsImplemented(string key);

    /// <summary>Every implementation of a kind. How an operator discovers what they may configure.</summary>
    IReadOnlyList<IRankingStrategy> OfKind(RankingStrategyKind kind);
}
