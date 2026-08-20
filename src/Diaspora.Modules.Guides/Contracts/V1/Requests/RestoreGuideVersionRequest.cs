namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// Applies an old version's content back onto the draft. Not time travel for readers — the
/// restore is an edit like any other, and publishing afterwards is what mints a new version.
/// </summary>
public sealed record RestoreGuideVersionRequest
{
    /// <summary>The revision the editor loaded. Null opts out of the conflict check.</summary>
    public int? BaseRevision { get; init; }
}
