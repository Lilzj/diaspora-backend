namespace Diaspora.Modules.Guides.Domain.CountryContext;

/// <summary>
/// How much of one kind of thing the platform holds for one country.
/// <para>
/// <b>A key and a number, and the poverty of that shape is the point.</b> This row exists so a
/// country dashboard can say "142 jobs" beside "38 guides" without B8.1 knowing what a job is. It
/// holds no title, no identifier, no category and no link — because the moment it held any of
/// those, the Knowledge Directory would be maintaining a partial copy of the Jobs module, and a
/// stale one.
/// </para>
/// <para>
/// <b>The kind is a string, not a foreign key or an enum column.</b> A vertical that lands after
/// B8.1 publishes a contract this module subscribes to, or ships a context contributor of its own,
/// and its counter appears without a migration here. That is the same open-set discipline the
/// section and category seams use, applied to numbers.
/// </para>
/// </summary>
public sealed class CountryContextCounter
{
    private CountryContextCounter(string kind)
    {
        Kind = kind;
    }

    private CountryContextCounter() { }

    /// <summary>A lower-case kind name — <c>jobs</c>, <c>housing</c>. Half of the row's identity.</summary>
    public string Kind { get; private set; } = string.Empty;

    /// <summary>
    /// How many there are.
    /// <para>
    /// Never allowed below zero. Counters maintained from at-least-once delivery drift: a
    /// duplicate decrement, an event replayed after a redeploy, a vertical that publishes an
    /// archive for something it never published. Clamping at zero means the worst case is a
    /// slightly low number rather than a country dashboard reporting minus three homes.
    /// </para>
    /// </summary>
    public int Count { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    internal static CountryContextCounter Start(string kind) => new(kind);

    /// <summary>Applies a delta, clamped at zero. Returns the value after the change.</summary>
    internal int Apply(int delta, DateTimeOffset at)
    {
        Count = Math.Max(0, Count + delta);
        UpdatedAt = at;

        return Count;
    }

    /// <summary>Replaces the value outright. For a rebuild from an authoritative count.</summary>
    internal int Set(int count, DateTimeOffset at)
    {
        Count = Math.Max(0, count);
        UpdatedAt = at;

        return Count;
    }
}
