using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Reactions;

namespace Diaspora.Modules.Community.Extensibility.Reactions;

/// <summary>
/// The reaction kinds available in this process, in memory.
/// <para>
/// A cache over the definition table, held for the lifetime of a request. Reaction definitions
/// are read on every reaction, every summary projection and every ranking pass; re-reading a
/// table of five rows on each of those is the sort of query that does not show up in a profile
/// individually and dominates it in aggregate.
/// </para>
/// </summary>
public sealed class ReactionCatalog
{
    private readonly Dictionary<string, ReactionDefinition> _byKey =
        new(StringComparer.OrdinalIgnoreCase);

    private bool _loaded;

    public IReadOnlyCollection<ReactionDefinition> Definitions => _byKey.Values;

    public bool IsLoaded => _loaded;

    public void Load(IEnumerable<ReactionDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        _byKey.Clear();

        foreach (var definition in definitions.Where(definition => definition.IsEnabled))
        {
            _byKey[definition.Key] = definition;
        }

        _loaded = true;
    }

    public ReactionDefinition? Find(string? key) =>
        string.IsNullOrWhiteSpace(key) ? null : _byKey.GetValueOrDefault(key.Trim());

    /// <summary>The kinds offered on a given content type, in display order.</summary>
    public IReadOnlyList<ReactionDefinition> For(CommunityContentType contentType) =>
        _byKey.Values
            .Where(definition => definition.Applies(contentType))
            .OrderBy(definition => definition.Position)
            .ThenBy(definition => definition.Key, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// The ranking weight for a key, or one.
    /// <para>
    /// One rather than zero for an unknown key: a reaction whose definition has been retired
    /// still happened, and scoring it as nothing would silently rewrite the ranking of every
    /// post that carries one.
    /// </para>
    /// </summary>
    public double WeightOf(string key) => Find(key)?.Weight ?? 1d;
}
