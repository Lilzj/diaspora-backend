namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// One row in a submitted arrangement.
/// <para>
/// <b>There is no position field, deliberately.</b> Position comes from the order of the list, and
/// a caller-supplied one is a caller-supplied bug: two rows claiming position 3, a gap at 7 after a
/// delete, an ordering that renders differently from the list somebody dragged. The server
/// renumbers contiguously from zero, so what is stored is always what was arranged.
/// </para>
/// </summary>
public sealed record CountryDirectoryEntryInput
{
    /// <summary>The catalogue key to place. Must exist and must not be archived.</summary>
    public required string KnowledgeCategoryKey { get; init; }

    /// <summary>This country's own wording for the row, or null to use the catalogue label.</summary>
    public string? LabelOverride { get; init; }

    /// <summary>Arranged but not shown. For a row whose guides have not been written yet.</summary>
    public bool IsHidden { get; init; }
}
