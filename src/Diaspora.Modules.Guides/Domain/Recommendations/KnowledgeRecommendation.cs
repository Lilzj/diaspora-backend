using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Events;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

// The kernel's legacy identifier bag (Diaspora.SharedKernel.Identity) also declares a GuideId.
// The module-local identifier is the authoritative one for B8; the alias resolves the
// collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Domain.Recommendations;

/// <summary>
/// What a curator says to read after one particular guide.
/// <para>
/// <b>An aggregate per source guide, and it is not the guide.</b> Recommendations are editorial
/// metadata <em>about</em> a guide, written by somebody who is usually not its author, changed on
/// a completely different rhythm from the content. Storing them on the guide would mean loading,
/// locking and bumping the revision of a published document every time an editor adjusted a
/// suggestion box under it — and would give the guide's author an edit conflict caused by
/// somebody else's curation.
/// </para>
/// <para>
/// <b>Editor-managed today, and the shape does not presume otherwise.</b> When recommendations are
/// eventually generated, they arrive as a different source populating the same rows; nothing above
/// this aggregate learns that the list stopped being hand-written. That is the whole reason the
/// curated version is worth building first.
/// </para>
/// </summary>
public sealed class KnowledgeRecommendation
    : AggregateRoot<KnowledgeRecommendationId>, IAuditableEntity
{
    private readonly List<RecommendedRead> _reads = [];

    private KnowledgeRecommendation(
        KnowledgeRecommendationId id, GuideId sourceGuideId, DateTimeOffset createdAt)
        : base(id)
    {
        SourceGuideId = sourceGuideId;
        CreatedAt = createdAt;
    }

    private KnowledgeRecommendation() { }

    /// <summary>The guide somebody just finished. Unique — one recommendation set per guide.</summary>
    public GuideId SourceGuideId { get; private set; } = default!;

    /// <summary>The optimistic concurrency token.</summary>
    public int Revision { get; private set; }

    /// <summary>The next reads, in curated order.</summary>
    public IReadOnlyList<RecommendedRead> Reads => _reads.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static Result<KnowledgeRecommendation> Create(
        KnowledgeRecommendationId id, GuideId sourceGuideId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(sourceGuideId);

        return Result.Success(new KnowledgeRecommendation(id, sourceGuideId, now));
    }

    /// <summary>
    /// Replaces the whole list, in the order given.
    /// <para>
    /// An empty list is valid and means "nothing follows this" — which a curator sometimes
    /// genuinely means, and which must be expressible without deleting the aggregate and losing
    /// the audit trail of who decided it.
    /// </para>
    /// </summary>
    public Result SetReads(
        IEnumerable<(Guid GuideId, string? Reason)>? reads, UserId? changedBy)
    {
        var requested = reads?.ToList() ?? [];

        if (requested.Count > KnowledgeLimits.MaxRecommendedReads)
        {
            return Result.Failure(KnowledgeErrors.RecommendationLimit);
        }

        var resolved = new List<RecommendedRead>(requested.Count);
        var seen = new HashSet<Guid>();
        var position = 0;

        foreach (var (guideId, reason) in requested)
        {
            if (guideId == SourceGuideId.Value)
            {
                return Result.Failure(KnowledgeErrors.RecommendationSelf);
            }

            var read = RecommendedRead.TryCreate(guideId, position, reason);

            if (read is null)
            {
                return Result.Failure(KnowledgeErrors.GuideNotDiscoverable);
            }

            // A repeated guide is collapsed rather than rejected: the curator meant it once, and
            // failing the whole list over a duplicate paste helps nobody.
            if (!seen.Add(guideId))
            {
                continue;
            }

            resolved.Add(read);
            position++;
        }

        _reads.Clear();
        _reads.AddRange(resolved);

        Revision++;

        RaiseDomainEvent(new RecommendedReadsChangedDomainEvent
        {
            RecommendationId = Id,
            SourceGuideId = SourceGuideId,
            RecommendedCount = _reads.Count,
            ChangedBy = changedBy
        });

        return Result.Success();
    }

    /// <inheritdoc cref="Knowledge.KnowledgeCategory.EnsureRevision" />
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
}
