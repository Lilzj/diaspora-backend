namespace Diaspora.Modules.Community.Contracts.V1.Requests;

/// <summary>A member-named folder of saved things.</summary>
public sealed record CreateBookmarkFolderRequest
{
    public required string Name { get; init; }

    /// <summary>An emoji. The client renders one beside every collection.</summary>
    public string? Glyph { get; init; }
}
