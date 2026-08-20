using Diaspora.Modules.Guides.Domain.Knowledge;
using Diaspora.Modules.Guides.Domain.Policies;

namespace Diaspora.Modules.Guides.Domain.Recommendations;

/// <summary>
/// One guide a curator says to read next.
/// <para>
/// An identifier, a position and a sentence of reasoning. Nothing else, and in particular no
/// score: the ordering is a human judgement about what follows what, and attaching a number to it
/// would invite somebody to compute the number later, which is precisely the AI sequencing this
/// phase is scoped to keep out.
/// </para>
/// </summary>
public sealed class RecommendedRead
{
    private RecommendedRead(Guid guideId, int position, string? reason)
    {
        GuideId = guideId;
        Position = position;
        Reason = reason;
    }

    private RecommendedRead() { }

    /// <summary>A B8 guide identifier. Resolved at read time, never cached here.</summary>
    public Guid GuideId { get; private set; }

    /// <summary>Contiguous from zero. Renumbered by the aggregate on every change.</summary>
    public int Position { get; private set; }

    /// <summary>Why this comes next. Rendered under the card, verbatim.</summary>
    public string? Reason { get; private set; }

    internal static RecommendedRead? TryCreate(Guid guideId, int position, string? reason) =>
        guideId == Guid.Empty
            ? null
            : new RecommendedRead(
                guideId,
                position,
                KnowledgeNormalization.Text(reason, KnowledgeLimits.MaxCuratorNote));
}
