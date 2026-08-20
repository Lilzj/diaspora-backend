using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

// The kernel's legacy identifier bag (Diaspora.SharedKernel.Identity) also declares a GuideId.
// The module-local identifier is the authoritative one for B8; the alias resolves the
// collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Domain.Drafts;

/// <summary>
/// One auto-saved moment of a guide's draft — the "my browser crashed" insurance.
/// <para>
/// A separate aggregate rather than history on the guide, because auto-save fires every few
/// seconds of typing and must be the cheapest possible write: one insert, no aggregate load,
/// no invariants beyond "there is content". The snapshot is opaque JSON written by the same
/// serializer versions use. The saving handler prunes to the newest
/// <see cref="Policies.GuideLimits.MaxDraftSnapshots"/> per guide — pruning is a storage
/// policy, not a domain rule, so it lives with the writer.
/// </para>
/// </summary>
public sealed class GuideDraftSnapshot : AggregateRoot<GuideDraftSnapshotId>
{
    private GuideDraftSnapshot(
        GuideDraftSnapshotId id,
        GuideId guideId,
        string snapshot,
        UserId savedBy,
        DateTimeOffset savedAt)
        : base(id)
    {
        GuideId = guideId;
        Snapshot = snapshot;
        SavedBy = savedBy;
        SavedAt = savedAt;
    }

    private GuideDraftSnapshot() { }

    public GuideId GuideId { get; private set; } = default!;

    /// <summary>The draft as JSON, in the snapshot serializer's shape. Opaque here.</summary>
    public string Snapshot { get; private set; } = string.Empty;

    public UserId SavedBy { get; private set; } = default!;

    public DateTimeOffset SavedAt { get; private set; }

    public static Result<GuideDraftSnapshot> Create(
        GuideDraftSnapshotId id,
        GuideId guideId,
        string? snapshot,
        UserId savedBy,
        DateTimeOffset savedAt)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(guideId);
        ArgumentNullException.ThrowIfNull(savedBy);

        return string.IsNullOrWhiteSpace(snapshot)
            ? Result.Failure<GuideDraftSnapshot>(GuideErrors.SnapshotInvalid)
            : Result.Success(new GuideDraftSnapshot(id, guideId, snapshot, savedBy, savedAt));
    }
}
