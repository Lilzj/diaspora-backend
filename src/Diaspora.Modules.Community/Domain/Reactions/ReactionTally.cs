using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Reactions;

/// <summary>One reaction kind's count on one piece of content.</summary>
public sealed class ReactionTally : Entity<ReactionTallyId>
{
    private ReactionTally(
        ReactionTallyId id, string reactionKey, ReactionSentiment sentiment, int count)
        : base(id)
    {
        ReactionKey = reactionKey;
        Sentiment = sentiment;
        Count = count;
    }

    private ReactionTally() { }

    public string ReactionKey { get; private set; } = string.Empty;

    public ReactionSentiment Sentiment { get; private set; }

    public int Count { get; private set; }

    internal static ReactionTally Start(string reactionKey, ReactionSentiment sentiment) =>
        new(ReactionTallyId.New(), reactionKey, sentiment, 0);

    internal void Adjust(int delta) => Count = Math.Max(0, Count + delta);
}
