namespace Diaspora.Modules.Community.Contracts.V1.Responses;

/// <summary>
/// One tile on the saved screen.
/// <para>
/// Serves both kinds of collection the client and the brief ask for: a type-derived group
/// (providers, guides, jobs) has a <c>contentType</c> and no <c>folderId</c>; a member-named
/// folder has a <c>folderId</c> and no type. One shape, because the screen renders them the same
/// way.
/// </para>
/// </summary>
public sealed record BookmarkCollectionResponse
{
    public string? ContentType { get; init; }

    public Guid? FolderId { get; init; }

    public required string Label { get; init; }

    public string? Icon { get; init; }

    public required int Count { get; init; }

    public required bool IsAvailable { get; init; }

    /// <summary>Media identifiers for the tile preview. Never URLs.</summary>
    public required IReadOnlyList<Guid> ThumbnailMediaIds { get; init; }
}
