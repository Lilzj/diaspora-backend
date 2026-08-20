using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Reactions;

/// <summary>
/// The per-kind reaction counts for one piece of content.
/// <para>
/// One row per content item, owning one tally per reaction kind that has ever been used on it.
/// This shape rather than a count column per kind, because the kinds are data: a column per kind
/// would mean a migration every time a deployment added a reaction, which is the exact coupling
/// a configurable catalogue exists to remove.
/// </para>
/// <para>
/// It is also what makes the client's <c>helpfulCount</c>, <c>confirmationCount</c> and
/// <c>likeCount</c> renderable from one lookup instead of three aggregate queries, and what a
/// ranking strategy reads to compute a weighted score without touching the reaction table.
/// </para>
/// </summary>
public sealed class ReactionSummary : AggregateRoot<ReactionSummaryId>, IAuditableEntity
{
    private readonly List<ReactionTally> _tallies = [];

    private ReactionSummary(ReactionSummaryId id, ContentReference content, string contentKey)
        : base(id)
    {
        Content = content;
        ContentKey = contentKey;
    }

    private ReactionSummary() { }

    public ContentReference Content { get; private set; } = default!;

    /// <summary>
    /// The reference in single-column form. Denormalised because a unique index cannot span an
    /// owned type's columns — the same constraint B5 hit with its collection owner key.
    /// </summary>
    public string ContentKey { get; private set; } = string.Empty;

    public int TotalCount { get; private set; }

    /// <summary>Sum of count times definition weight. The number a ranking strategy wants.</summary>
    public double WeightedScore { get; private set; }

    public IReadOnlyList<ReactionTally> Tallies => _tallies.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }

    public static ReactionSummary For(ContentReference content)
    {
        ArgumentNullException.ThrowIfNull(content);

        return new ReactionSummary(ReactionSummaryId.New(), content, content.Key);
    }

    public int CountOf(string reactionKey) =>
        _tallies.Find(tally =>
            string.Equals(tally.ReactionKey, reactionKey, StringComparison.Ordinal))?.Count ?? 0;

    public int CountBySentiment(ReactionSentiment sentiment) =>
        _tallies.Where(tally => tally.Sentiment == sentiment).Sum(tally => tally.Count);

    /// <summary>
    /// Moves one kind's count and re-derives the totals.
    /// <para>
    /// The weight arrives as an argument rather than being read from a definition, so that the
    /// summary aggregate holds no reference to the catalogue. A summary that had to load the
    /// definitions to recompute itself would make every reaction a two-aggregate transaction.
    /// </para>
    /// </summary>
    public void Adjust(string reactionKey, ReactionSentiment sentiment, double weight, int delta)
    {
        var tally = _tallies.Find(candidate =>
            string.Equals(candidate.ReactionKey, reactionKey, StringComparison.Ordinal));

        if (tally is null)
        {
            if (delta <= 0)
            {
                return;
            }

            tally = ReactionTally.Start(reactionKey, sentiment);
            _tallies.Add(tally);
        }

        tally.Adjust(delta);

        TotalCount = Math.Max(0, TotalCount + delta);
        WeightedScore = Math.Max(0, WeightedScore + (weight * delta));
    }

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
