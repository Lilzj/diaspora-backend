using Diaspora.Modules.Community.Domain.ValueObjects;

namespace Diaspora.Modules.Community.Extensibility.Feeds;

/// <summary>One page of a feed, and where the next one starts.</summary>
public sealed record FeedPage
{
    public required IReadOnlyList<FeedItem> Items { get; init; }

    /// <summary>Null when this is the last page. The client stops asking.</summary>
    public FeedCursor? NextCursor { get; init; }

    public bool HasMore => NextCursor is not null;

    /// <summary>
    /// Optional. Almost always null, and that is correct: counting the whole of a feed costs a
    /// full scan, and no client renders the number. A provider that can produce one cheaply may.
    /// </summary>
    public int? TotalAvailable { get; init; }

    public static FeedPage Empty { get; } = new() { Items = [] };

    public static FeedPage Of(IReadOnlyList<FeedItem> items, FeedCursor? nextCursor) =>
        new() { Items = items, NextCursor = nextCursor };
}
