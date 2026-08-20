namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// The editor's auto-save: the current structure, snapshotted without ceremony.
/// <para>
/// Validation is deliberately lax compared with a real save — an auto-save that refused a
/// half-typed section would fight the writer every few seconds. The handler prunes snapshots to
/// the newest few per guide.
/// </para>
/// </summary>
public sealed record SaveGuideDraftRequest
{
    public IReadOnlyList<GuideSectionInput> Sections { get; init; } = [];

    /// <summary>The revision the editor loaded. Null opts out of the conflict check.</summary>
    public int? BaseRevision { get; init; }
}
