using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Events;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Knowledge;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Domain.LearningPaths;

/// <summary>
/// A curated route through one country's knowledge — "Arriving in Mauritius", in seven steps.
/// <para>
/// <b>Curated, and the brief is explicit that it stays that way.</b> No sequencing is inferred, no
/// model orders the steps, nothing here learns from what readers did next. A person who knows what
/// it is like to arrive somewhere with two suitcases decides that immigration comes before housing
/// and housing before banking, and writes it down. That ordering is knowledge, and it is exactly
/// the kind this platform exists to carry.
/// </para>
/// <para>
/// <b>A path is metadata over guides, never a copy of them.</b> Each step holds an identifier and
/// its own framing sentence; not one holds guide content. Everything that would make a path go
/// stale — a retitle, an edit, a new version — belongs to B8 and reaches a reader through B8.
/// </para>
/// <para>
/// <b>Three states, and archived is the terminal one rather than a soft delete.</b> This aggregate
/// deliberately does not implement <c>ISoftDeletable</c>: a path that was correct until a country
/// changed its visa rules is a record of what the advice used to be, and the platform keeps those.
/// Giving it both an archived status and a deleted flag would produce two ways to be gone and a
/// restore that fixes one of them — which is the confusion the category and directory aggregates
/// avoid by having exactly one.
/// </para>
/// </summary>
public sealed class LearningPath : AggregateRoot<LearningPathId>, IAuditableEntity
{
    private readonly List<LearningPathStep> _steps = [];

    private LearningPath(
        LearningPathId id,
        string key,
        string countryCode,
        string title,
        string? description,
        string? language,
        DateTimeOffset createdAt)
        : base(id)
    {
        Key = key;
        CountryCode = countryCode;
        Title = title;
        Description = description;
        Language = language;
        Status = LearningPathStatus.Draft;
        CreatedAt = createdAt;
    }

    private LearningPath() { }

    /// <summary>The stable key. Unique within its country, and what a deep link carries.</summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>Upper-case ISO 3166-1 alpha-2. A path always belongs to exactly one country.</summary>
    public string CountryCode { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>
    /// The language the path's own copy is written in.
    /// <para>
    /// One language per path, and no localization collection — unlike a category label, a path is
    /// a piece of writing rather than a term. A second language is a second path, which is honest:
    /// the French route through Mauritian bureaucracy legitimately has different steps from the
    /// English one, because different paperwork applies.
    /// </para>
    /// </summary>
    public string? Language { get; private set; }

    public LearningPathStatus Status { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    /// <summary>The optimistic concurrency token. Bumped on every mutation.</summary>
    public int Revision { get; private set; }

    /// <summary>The steps, in curated order.</summary>
    public IReadOnlyList<LearningPathStep> Steps => _steps.AsReadOnly();

    /// <summary>Steps that count towards completion. The denominator of a progress indicator.</summary>
    public int RequiredStepCount => _steps.Count(step => !step.IsOptional);

    /// <summary>Whether the path is coherent enough to show a newcomer.</summary>
    public bool IsPublishable => _steps.Count > 0 && !string.IsNullOrWhiteSpace(Title);

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Construction
    // ════════════════════════════════════════════════════════════════════════════════════════

    public static Result<LearningPath> Create(
        LearningPathId id,
        string? key,
        string? countryCode,
        string? title,
        DateTimeOffset now,
        string? description = null,
        string? language = null,
        UserId? createdBy = null)
    {
        ArgumentNullException.ThrowIfNull(id);

        var normalizedKey = KnowledgeNormalization.PathKey(key);

        if (normalizedKey is null)
        {
            return Result.Failure<LearningPath>(KnowledgeErrors.PathKeyInvalid);
        }

        var country = KnowledgeNormalization.Country(countryCode);

        if (country is null)
        {
            return Result.Failure<LearningPath>(KnowledgeErrors.CountryInvalid);
        }

        var trimmedTitle = KnowledgeNormalization.Text(title, KnowledgeLimits.MaxPathTitle);

        if (trimmedTitle is null)
        {
            return Result.Failure<LearningPath>(KnowledgeErrors.PathNotPublishable);
        }

        var path = new LearningPath(
            id,
            normalizedKey,
            country,
            trimmedTitle,
            KnowledgeNormalization.Text(description, KnowledgeLimits.MaxPathDescription),
            KnowledgeNormalization.Locale(language),
            now);

        path.RaiseChanged(KnowledgeChangeKinds.Created, createdBy);

        return Result.Success(path);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Curation
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Rewrites the path's own copy. The key and the country never change.</summary>
    public Result UpdateMetadata(
        string? title, string? description, string? language, UserId? changedBy)
    {
        if (Status == LearningPathStatus.Archived)
        {
            return Result.Failure(KnowledgeErrors.PathArchived);
        }

        var trimmedTitle = KnowledgeNormalization.Text(title, KnowledgeLimits.MaxPathTitle);

        if (trimmedTitle is null)
        {
            return Result.Failure(KnowledgeErrors.PathNotPublishable);
        }

        Title = trimmedTitle;
        Description = KnowledgeNormalization.Text(description, KnowledgeLimits.MaxPathDescription);
        Language = KnowledgeNormalization.Locale(language);

        Bump();
        RaiseChanged(KnowledgeChangeKinds.Updated, changedBy);

        return Result.Success();
    }

    /// <summary>
    /// Replaces the whole sequence, in the order given.
    /// <para>
    /// <b>Rearranging a published path is allowed, deliberately.</b> The alternative — unpublish,
    /// edit, republish — sounds safer and is worse: it takes the country's onboarding offline for
    /// however long the editor takes, and the most common edit by far is inserting a step somebody
    /// discovered was missing. A reader mid-path sees a longer path, which is exactly what
    /// happened.
    /// </para>
    /// <para>
    /// Step identifiers are caller-supplied and preserved, so a client tracking "you are on step 3"
    /// still knows which step that was after an insertion above it.
    /// </para>
    /// </summary>
    public Result Arrange(
        IEnumerable<(Guid StepId, string? Title, string? Description, Guid? GuideId,
            string? KnowledgeCategoryKey, bool IsOptional)>? steps,
        UserId? changedBy)
    {
        if (Status == LearningPathStatus.Archived)
        {
            return Result.Failure(KnowledgeErrors.PathArchived);
        }

        var requested = steps?.ToList() ?? [];

        if (requested.Count > KnowledgeLimits.MaxPathSteps)
        {
            return Result.Failure(KnowledgeErrors.StepLimit);
        }

        var resolved = new List<LearningPathStep>(requested.Count);
        var seen = new HashSet<Guid>();
        var position = 0;

        foreach (var (stepId, title, description, guideId, categoryKey, isOptional) in requested)
        {
            var step = LearningPathStep.TryCreate(
                stepId, position, title, description, guideId, categoryKey, isOptional);

            if (step is null)
            {
                return Result.Failure(KnowledgeErrors.StepInvalid);
            }

            if (!seen.Add(step.StepId))
            {
                return Result.Failure(KnowledgeErrors.StepInvalid);
            }

            resolved.Add(step);
            position++;
        }

        _steps.Clear();
        _steps.AddRange(resolved);

        Bump();
        RaiseChanged(KnowledgeChangeKinds.Arranged, changedBy);

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Lifecycle
    // ════════════════════════════════════════════════════════════════════════════════════════

    public Result Publish(DateTimeOffset now, UserId? changedBy)
    {
        if (Status == LearningPathStatus.Published)
        {
            return Result.Failure(KnowledgeErrors.PathAlreadyPublished);
        }

        if (!IsPublishable)
        {
            return Result.Failure(KnowledgeErrors.PathNotPublishable);
        }

        Status = LearningPathStatus.Published;
        PublishedAt ??= now;

        Bump();
        RaiseChanged(KnowledgeChangeKinds.Published, changedBy);

        return Result.Success();
    }

    /// <summary>
    /// Withdraws the path. The steps stay, because they record what the advice used to be.
    /// </summary>
    public Result Archive(UserId? changedBy)
    {
        if (Status == LearningPathStatus.Archived)
        {
            return Result.Success();
        }

        Status = LearningPathStatus.Archived;

        Bump();
        RaiseChanged(KnowledgeChangeKinds.Archived, changedBy);

        return Result.Success();
    }

    /// <summary>
    /// Brings an archived path back to the curator's desk, as a draft.
    /// <para>
    /// Never straight back to published, for the same reason a restored country directory is not
    /// republished: it was archived because somebody decided it was wrong, and putting it back in
    /// front of newcomers is a second decision that deserves a second look.
    /// </para>
    /// </summary>
    public Result Restore(UserId? changedBy)
    {
        if (Status != LearningPathStatus.Archived)
        {
            return Result.Failure(KnowledgeErrors.PathNotPublishable);
        }

        Status = LearningPathStatus.Draft;

        Bump();
        RaiseChanged(KnowledgeChangeKinds.Restored, changedBy);

        return Result.Success();
    }

    /// <inheritdoc cref="KnowledgeCategory.EnsureRevision" />
    public Result EnsureRevision(int? expectedRevision) =>
        expectedRevision is null || expectedRevision == Revision
            ? Result.Success()
            : Result.Failure(KnowledgeErrors.RevisionConflict);

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

    private void Bump() => Revision++;

    private void RaiseChanged(string change, UserId? changedBy) =>
        RaiseDomainEvent(new LearningPathChangedDomainEvent
        {
            LearningPathId = Id,
            Key = Key,
            CountryCode = CountryCode,
            Title = Title,
            Change = change,
            Status = Status,
            StepCount = _steps.Count,
            ChangedBy = changedBy
        });
}
