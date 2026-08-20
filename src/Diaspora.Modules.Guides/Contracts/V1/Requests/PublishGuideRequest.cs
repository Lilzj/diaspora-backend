namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// Publishes the current draft as the next version.
/// <para>
/// The notes are the editor's own account of what changed — the version list renders them, and
/// a history whose entries all say nothing is a history nobody can navigate.
/// </para>
/// </summary>
public sealed record PublishGuideRequest
{
    public string? RevisionNotes { get; init; }

    /// <summary>The revision the editor loaded. Null opts out of the conflict check.</summary>
    public int? BaseRevision { get; init; }
}
