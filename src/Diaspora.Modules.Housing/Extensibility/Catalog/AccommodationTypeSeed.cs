namespace Diaspora.Modules.Housing.Extensibility.Catalog;

/// <summary>
/// One accommodation type a contributor supplies.
/// <para>
/// <see cref="IsShared"/> and <see cref="IsTemporary"/> are the two flags that carry behaviour
/// rather than presentation: shared is what permits roommate preferences and a gender preference,
/// temporary is what a member looking for somewhere to stay for six weeks filters on. A
/// contributor adding a type must say which it is, because Housing cannot infer either from a name
/// it has never seen.
/// </para>
/// </summary>
public sealed record AccommodationTypeSeed
{
    /// <summary>The stable key listings will store. Normalised on the way in.</summary>
    public required string Key { get; init; }

    /// <summary>What a member sees. Editable by staff afterwards and never overwritten by a reseed.</summary>
    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>An icon key the client resolves. A name, never a URL.</summary>
    public string? IconKey { get; init; }

    /// <summary>Whether somebody else lives there. Permits roommate matching.</summary>
    public bool IsShared { get; init; }

    /// <summary>Whether this is somewhere to stay rather than somewhere to live.</summary>
    public bool IsTemporary { get; init; }

    /// <summary>Where it sorts in a picker. Lower first.</summary>
    public int Order { get; init; }
}
