using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Reactions;

namespace Diaspora.Modules.Community.Extensibility.Reactions;

/// <summary>
/// The reactions B6 ships.
/// <para>
/// Four of the five are the client's own vocabulary, taken verbatim from the reaction type it
/// declares: helpful, confirmed and useful. Its fourth, <c>Save</c>, is deliberately <b>not</b>
/// a reaction here — saving is a bookmark, with folders, a private note and its own screen, and
/// modelling it as a reaction would mean the saved screen and the reaction count disagreed about
/// what a save was. That is intentional deviation D-6 in the frontend contract report.
/// </para>
/// <para>
/// Two are added. A plain <c>like</c>, because the client's comment model has a
/// <c>likeCount</c> and nothing to produce it. And <c>disputed</c>, because a corroboration
/// system with no way to say "this is wrong" turns every disagreement into a report, which is a
/// moderator's problem rather than a reader's signal.
/// </para>
/// <para>
/// The weights are the deliberate part. A confirmation is worth four likes to a ranker, because
/// somebody staking their name on a safety claim being true is a stronger signal than somebody
/// tapping a heart. A dispute is weighted at zero: it must not add to an engagement score, or
/// arguing about a post promotes it.
/// </para>
/// </summary>
public sealed class DefaultReactionCatalogContributor : IReactionCatalogContributor
{
    public string Module => CommunityModuleMetadata.Name;

    public IEnumerable<ReactionDefinition> GetDefinitions()
    {
        yield return ReactionDefinition.Create(
            "like", "Like", "\U0001F44D", ReactionSentiment.Positive, 1d,
            position: 0, isBuiltIn: true);

        yield return ReactionDefinition.Create(
            "helpful", "Helpful", "\U0001F44D", ReactionSentiment.Positive, 2d,
            position: 1, isBuiltIn: true);

        yield return ReactionDefinition.Create(
            "useful", "Useful", "\U0001F4A1", ReactionSentiment.Positive, 2d,
            position: 2, isBuiltIn: true);

        // Only on things that make a factual claim. Confirming a comment means nothing, and
        // offering the control there teaches members it means nothing everywhere.
        yield return ReactionDefinition.Create(
            "confirmed", "I can confirm this", "\u2705", ReactionSentiment.Corroborating, 4d,
            appliesTo: [CommunityContentType.Post, CommunityContentType.Guide],
            position: 3,
            isBuiltIn: true);

        yield return ReactionDefinition.Create(
            "disputed", "This is not accurate", "\u26A0", ReactionSentiment.Disputing, 0d,
            appliesTo: [CommunityContentType.Post, CommunityContentType.Guide],
            position: 4,
            isBuiltIn: true);
    }
}
