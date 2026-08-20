namespace Diaspora.Modules.Location.Extensibility;

/// <summary>One vertical's answer to a proximity question.</summary>
public sealed record NearbyResourceGroup
{
    /// <summary>
    /// The key this group appears under in the response. The frontend's existing mock returns
    /// <c>housing</c>, <c>jobs</c>, <c>marketplace</c>, <c>events</c>, <c>providers</c> and
    /// <c>updates</c>; a provider claiming one of those names produces a byte-identical
    /// response shape.
    /// </summary>
    public required string ResourceType { get; init; }

    /// <summary>Nearest first. Location re-sorts defensively; a provider should still order.</summary>
    public required IReadOnlyList<NearbyResourceItem> Items { get; init; }

    /// <summary>
    /// How many the vertical has in range in total, when it is cheap to know. Null rather
    /// than a guess — a wrong total is worse than none, because the client renders it.
    /// </summary>
    public int? TotalAvailable { get; init; }

    public static NearbyResourceGroup Empty(string resourceType) =>
        new() { ResourceType = resourceType, Items = [] };
}
