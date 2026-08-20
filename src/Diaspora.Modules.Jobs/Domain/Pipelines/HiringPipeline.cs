using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Domain.Pipelines;

/// <summary>
/// The board an employer moves candidates across for one posting.
/// <para>
/// <b>Per posting rather than per employer, and that is a real choice.</b> Hiring a night-shift
/// carer and hiring a finance director are not the same process, and an employer forced to share
/// one board across both ends up with a lowest-common-denominator board they work around in a
/// spreadsheet. Defaults come from a template, so the common case still costs nobody anything.
/// </para>
/// <para>
/// <b>Two stages can never be removed.</b> Every application enters at
/// <see cref="HiringStageKind.Applied"/> and a successful one ends at
/// <see cref="HiringStageKind.Hired"/>; a pipeline missing either has nowhere to put a
/// candidate, and it is a state easy to reach by accident and impossible to leave gracefully.
/// </para>
/// <para>
/// <b>Applications reference stages by key and the pipeline never renumbers them.</b> Renaming
/// and reordering are free; removing a stage that applications sit in is the handler's problem
/// to resolve, because only it can see them.
/// </para>
/// </summary>
public sealed class HiringPipeline : AggregateRoot<HiringPipelineId>, IAuditableEntity, IHasOwner
{
    private readonly List<HiringStage> _stages = [];

    private HiringPipeline(
        HiringPipelineId id,
        JobPostingId postingId,
        EmployerProfileId employerId,
        DateTimeOffset createdAt)
        : base(id)
    {
        PostingId = postingId;
        EmployerId = employerId;
        CreatedAt = createdAt;
    }

    private HiringPipeline() { }

    public JobPostingId PostingId { get; private set; } = default!;

    public EmployerProfileId EmployerId { get; private set; } = default!;

    /// <summary>The platform's ownership seam.</summary>
    public Guid OwnerId => EmployerId.Value;

    /// <summary>The optimistic concurrency token.</summary>
    public int Revision { get; private set; }

    public IReadOnlyList<HiringStage> Stages =>
        _stages.OrderBy(stage => stage.Order).ToList().AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    /// <summary>The stage an application enters at.</summary>
    public HiringStage? EntryStage =>
        _stages.Where(stage => stage.Kind == HiringStageKind.Applied)
            .OrderBy(stage => stage.Order)
            .FirstOrDefault();

    /// <summary>The stage a successful application ends at.</summary>
    public HiringStage? HiredStage =>
        _stages.Find(stage => stage.Kind == HiringStageKind.Hired);

    /// <summary>Whether a stage key exists on this board.</summary>
    public bool HasStage(string? stageKey)
    {
        var key = HiringStage.NormalizeKey(stageKey);

        return key is not null
            && _stages.Exists(stage => string.Equals(stage.Key, key, StringComparison.Ordinal));
    }

    /// <summary>One stage by key, or null.</summary>
    public HiringStage? FindStage(string? stageKey)
    {
        var key = HiringStage.NormalizeKey(stageKey);

        return key is null
            ? null
            : _stages.Find(stage => string.Equals(stage.Key, key, StringComparison.Ordinal));
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Creation
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates a pipeline with the platform's default board: applied, screening, interview,
    /// offer, hired, rejected.
    /// <para>
    /// A default rather than an empty board, because an employer who has to design a hiring
    /// process before they can read their first application will not do either.
    /// </para>
    /// </summary>
    public static Result<HiringPipeline> CreateDefault(
        HiringPipelineId id,
        JobPostingId postingId,
        EmployerProfileId employerId,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(postingId);
        ArgumentNullException.ThrowIfNull(employerId);

        var pipeline = new HiringPipeline(id, postingId, employerId, now);

        pipeline._stages.AddRange(
        [
            HiringStage.Create("applied", "Applied", HiringStageKind.Applied, 0, isRemovable: false)!,
            HiringStage.Create("screening", "Screening", HiringStageKind.Screening, 1)!,
            HiringStage.Create("interview", "Interview", HiringStageKind.Interview, 2)!,
            HiringStage.Create("offer", "Offer", HiringStageKind.Offer, 3)!,
            HiringStage.Create("hired", "Hired", HiringStageKind.Hired, 4, isRemovable: false)!,
            HiringStage.Create("rejected", "Not proceeding", HiringStageKind.Rejected, 5)!
        ]);

        return Result.Success(pipeline);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Editing
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Replaces the board wholesale, preserving the keys of stages the editor sent back.
    /// <para>
    /// Wholesale because that is how a board editor works — drag, rename, delete, save — and a
    /// per-stage API would make the common case several round trips with a partial-failure
    /// story. The two mandatory kinds are re-added if the editor dropped them, rather than the
    /// save being refused: a client bug must not be able to produce a board with nowhere to put
    /// an application.
    /// </para>
    /// </summary>
    public Result ReplaceStages(IReadOnlyList<HiringStageDraft> stages, int? baseRevision)
    {
        ArgumentNullException.ThrowIfNull(stages);

        var revision = EnsureRevision(baseRevision);

        if (revision.IsFailure)
        {
            return revision;
        }

        if (stages.Count == 0)
        {
            return Result.Failure(JobErrors.PipelineEmpty);
        }

        if (stages.Count > JobLimits.MaxStages)
        {
            return Result.Failure(JobErrors.StageLimit);
        }

        var rebuilt = new List<HiringStage>(stages.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var draft in stages)
        {
            if (draft is null)
            {
                return Result.Failure(JobErrors.StageKeyInvalid);
            }

            var stage = HiringStage.Create(
                draft.Key,
                draft.Name,
                draft.Kind,
                rebuilt.Count,
                isRemovable: draft.Kind is not (HiringStageKind.Applied or HiringStageKind.Hired));

            if (stage is null)
            {
                return Result.Failure(JobErrors.StageKeyInvalid);
            }

            if (!seen.Add(stage.Key))
            {
                return Result.Failure(JobErrors.StageKeyTaken);
            }

            rebuilt.Add(stage);
        }

        EnsureMandatory(rebuilt, HiringStageKind.Applied, "applied", "Applied", atStart: true);
        EnsureMandatory(rebuilt, HiringStageKind.Hired, "hired", "Hired", atStart: false);

        for (var index = 0; index < rebuilt.Count; index++)
        {
            rebuilt[index].Reorder(index);
        }

        _stages.Clear();
        _stages.AddRange(rebuilt);

        Touch();

        return Result.Success();
    }

    /// <summary>Renames one stage. Its key, and every application pointing at it, are untouched.</summary>
    public Result RenameStage(string? stageKey, string? name)
    {
        var stage = FindStage(stageKey);

        if (stage is null)
        {
            return Result.Failure(JobErrors.StageNotFound);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(JobErrors.StageKeyInvalid);
        }

        var display = name.Trim();

        if (display.Length > JobLimits.MaxDisplayName)
        {
            display = display[..JobLimits.MaxDisplayName].TrimEnd();
        }

        stage.Rename(display);
        Touch();

        return Result.Success();
    }

    /// <summary>
    /// Removes a stage. Refuses the two mandatory kinds; whether applications sit in it is the
    /// handler's check, because only it can see them.
    /// </summary>
    public Result RemoveStage(string? stageKey)
    {
        var stage = FindStage(stageKey);

        if (stage is null)
        {
            return Result.Failure(JobErrors.StageNotFound);
        }

        if (!stage.IsRemovable)
        {
            return Result.Failure(JobErrors.StageNotRemovable);
        }

        _stages.Remove(stage);

        for (var index = 0; index < _stages.Count; index++)
        {
            _stages[index].Reorder(index);
        }

        Touch();

        return Result.Success();
    }

    /// <summary>Rejects a save based on a revision the caller no longer holds.</summary>
    public Result EnsureRevision(int? baseRevision) =>
        baseRevision is { } expected && expected != Revision
            ? Result.Failure(JobErrors.RevisionConflict)
            : Result.Success();

    /// <summary>Stamped by the shared persistence interceptor. Never called by a handler.</summary>
    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    /// <inheritdoc cref="SetCreated" />
    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    private void Touch() => Revision++;

    /// <summary>
    /// Puts back a mandatory stage the editor dropped. A client bug must not be able to produce
    /// a board with nowhere for an application to enter or end.
    /// </summary>
    private static void EnsureMandatory(
        List<HiringStage> stages, HiringStageKind kind, string key, string name, bool atStart)
    {
        if (stages.Exists(stage => stage.Kind == kind))
        {
            return;
        }

        var restored = HiringStage.Create(key, name, kind, 0, isRemovable: false)!;

        if (atStart)
        {
            stages.Insert(0, restored);
        }
        else
        {
            stages.Add(restored);
        }
    }
}
