using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Events;
using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.Modules.Recommendations.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Recommendations.Domain.Sets;

/// <summary>
/// One page of recommendations, as it was generated, for one member on one surface.
/// <para>
/// <b>Persisting the answer is what makes explainability a contract rather than a best effort.</b>
/// Without this table, "why am I seeing this" can only be answered by re-running the engine, which
/// re-reads a profile that has moved on and re-reads a catalogue an operator may have retuned — so
/// the answer would be plausible, defensible and not what happened. The brief asks for
/// explainability; this aggregate is what it costs.
/// </para>
/// <para>
/// <b>It is also the analytics denominator.</b> Every view, click, acceptance and dismissal is
/// reported against a set, which is how strategy performance is measured against what was actually
/// shown rather than against what would be generated now.
/// </para>
/// <para>
/// <b><see cref="PolicyExclusionCount"/> is recorded because nothing else can see it.</b> A surface
/// returning three items looks like a surface with nothing to show; a surface returning three items
/// after policies removed ninety is a misconfigured policy. The two are indistinguishable from the
/// outside and the difference is the whole of the diagnosis.
/// </para>
/// <para>
/// Soft-deletable and swept: a set is worth keeping for as long as somebody might tap "why", and
/// worth deleting after that. A member's recommendation history is one of the more revealing things
/// the platform holds and the retention window is deliberately short.
/// </para>
/// </summary>
public sealed class RecommendationSet : AggregateRoot<RecommendationSetId>, IAuditableEntity, ISoftDeletable
{
    private readonly List<RecommendationItem> _items = [];

    private RecommendationSet(
        RecommendationSetId id,
        UserId userId,
        RecommendationSurface surface,
        DateTimeOffset generatedAt,
        DateTimeOffset expiresAt) : base(id)
    {
        UserId = userId;
        Surface = surface;
        GeneratedAt = generatedAt;
        ExpiresAt = expiresAt;
    }

    private RecommendationSet() { }

    public UserId UserId { get; private set; } = default!;

    public RecommendationSurface Surface { get; private set; }

    /// <summary>Set when the surface asked for one kind of thing. Null on the home feed.</summary>
    public RecommendationTargetType? TargetType { get; private set; }

    public DateTimeOffset GeneratedAt { get; private set; }

    /// <summary>When a repeat request regenerates rather than reuses. Not when the row is deleted.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Whether the cold-start path produced this. The single most useful analytics dimension.</summary>
    public bool IsColdStart { get; private set; }

    /// <summary>How many candidates the pool held before policies ran.</summary>
    public int CandidateCount { get; private set; }

    /// <summary>How many a policy removed. See the class summary.</summary>
    public int PolicyExclusionCount { get; private set; }

    /// <summary>The strategy keys that contributed, comma-separated and sorted.</summary>
    public string StrategyKeys { get; private set; } = string.Empty;

    /// <summary>How long generation took. The performance budget's only honest witness.</summary>
    public int GenerationMilliseconds { get; private set; }

    public IReadOnlyList<RecommendationItem> Items => _items.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public string? DeletedBy { get; private set; }

    /// <summary>
    /// Records a generated page and announces it.
    /// <para>
    /// The only factory, and it takes the whole page. A set assembled item by item could be observed
    /// half-built, and a half-built page is one whose positions are wrong and whose analytics would
    /// count a surface as having shown three cards when it showed twenty.
    /// </para>
    /// </summary>
    public static RecommendationSet Generate(
        RecommendationSetId id,
        UserId userId,
        RecommendationSurface surface,
        IEnumerable<RecommendationItem> items,
        DateTimeOffset generatedAt,
        DateTimeOffset expiresAt,
        bool isColdStart,
        int candidateCount,
        int policyExclusionCount,
        int generationMilliseconds,
        RecommendationTargetType? targetType = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(items);

        var set = new RecommendationSet(id, userId, surface, generatedAt, expiresAt)
        {
            TargetType = targetType,
            IsColdStart = isColdStart,
            CandidateCount = Math.Max(0, candidateCount),
            PolicyExclusionCount = Math.Max(0, policyExclusionCount),
            GenerationMilliseconds = Math.Max(0, generationMilliseconds)
        };

        foreach (var item in items)
        {
            set._items.Add(item);
        }

        var strategies = set._items
            .SelectMany(item => item.Contributions.Select(contribution => contribution.StrategyKey)
                .DefaultIfEmpty(item.PrimaryStrategyKey))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        set.StrategyKeys = string.Join(',', strategies);

        set.RaiseDomainEvent(new RecommendationGeneratedDomainEvent
        {
            SetId = id,
            UserId = userId,
            Surface = surface,
            ItemCount = set._items.Count,
            StrategyKeys = strategies,
            IsColdStart = isColdStart,
            PolicyExclusionCount = set.PolicyExclusionCount,
            GeneratedAt = generatedAt
        });

        return set;
    }

    public bool IsFresh(DateTimeOffset asOf) => !IsDeleted && ExpiresAt > asOf;

    public RecommendationItem? FindItem(RecommendationTargetType targetType, Guid targetId) =>
        _items.Find(item => item.Target.TargetType == targetType && item.Target.TargetId == targetId);

    public IReadOnlyList<string> ParseStrategyKeys() =>
        StrategyKeys.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    public void MarkDeleted(DateTimeOffset timestamp, string? userId)
    {
        IsDeleted = true;
        DeletedAt = timestamp;
        DeletedBy = userId;
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
    }
}
