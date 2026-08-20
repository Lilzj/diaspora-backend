namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// Replaces a country directory's whole arrangement, in the order given.
/// <para>
/// Whole-list replacement rather than move/insert/remove operations. A drag-and-drop curation
/// screen already holds the final ordering; expressing it as a sequence of moves would mean the
/// client computing a diff and the server replaying it, with two chances to disagree about the
/// result and no way for either to notice.
/// </para>
/// </summary>
public sealed record ArrangeCountryDirectoryRequest
{
    /// <summary>The rows, in the order they should appear. An empty list clears the directory.</summary>
    public IReadOnlyList<CountryDirectoryEntryInput> Entries { get; init; } = [];

    /// <summary>The revision the client was looking at.</summary>
    public int? Revision { get; init; }
}
