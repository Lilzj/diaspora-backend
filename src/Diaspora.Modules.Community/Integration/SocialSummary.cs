using Diaspora.Modules.Community.Domain.ValueObjects;

namespace Diaspora.Modules.Community.Integration;

/// <summary>
/// Everything a consuming module needs to render social affordances on its own card.
/// <para>
/// The single shape B8, B9 and B13 read. A guide card showing a comment count, a reaction row
/// and a filled bookmark icon gets all three from one batched call, and never learns that
/// comments and reactions are different tables.
/// </para>
/// </summary>
public sealed record SocialSummary
{
    public required ContentReference Content { get; init; }

    public int CommentCount { get; init; }

    public int ReactionCount { get; init; }

    public int ShareCount { get; init; }

    public int BookmarkCount { get; init; }

    /// <summary>Per-kind counts, keyed by reaction key. Empty when nobody has reacted.</summary>
    public IReadOnlyDictionary<string, int> Reactions { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>Which kinds the caller reacted with. Empty for a signed-out reader.</summary>
    public IReadOnlyCollection<string> ViewerReactions { get; init; } = [];

    public bool ViewerHasBookmarked { get; init; }

    public static SocialSummary Empty(ContentReference content) => new() { Content = content };
}
