namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// The engagement counters a guide renders. Denormalised rollups — votes live in their own
/// rows, bookmarks and comments in Community — kept current by the handlers so a page of
/// twenty cards is one query.
/// </summary>
public sealed record GuideEngagementResponse
{
    public required long Views { get; init; }

    public required long UniqueViews { get; init; }

    public required int HelpfulCount { get; init; }

    public required int NotHelpfulCount { get; init; }

    public required int BookmarkCount { get; init; }

    public required int ShareCount { get; init; }

    public required int CommentCount { get; init; }
}
