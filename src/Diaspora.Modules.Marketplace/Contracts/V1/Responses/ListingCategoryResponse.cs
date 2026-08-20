namespace Diaspora.Modules.Marketplace.Contracts.V1.Responses;

/// <summary>
/// One node in the commerce taxonomy.
/// <para>
/// Sent flat with a <see cref="Path"/> rather than nested, so the client assembles whatever shape
/// its screen wants — a tree, a breadcrumb, a flat picker — from one response. A nested payload
/// would force a recursive renderer on every client for a structure three levels deep.
/// </para>
/// </summary>
public sealed record ListingCategoryResponse
{
    public required Guid CategoryId { get; init; }

    public required string Key { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? ParentKey { get; init; }

    /// <summary>The materialised path — <c>/electronics/mobile-phones/</c>.</summary>
    public required string Path { get; init; }

    public required int Depth { get; init; }

    /// <summary>An icon name the client resolves. The platform ships no images.</summary>
    public string? IconKey { get; init; }

    public required int Order { get; init; }

    /// <summary>Whether new listings may be filed here.</summary>
    public required bool IsActive { get; init; }

    /// <summary>Live listings filed directly here. What a category tile renders.</summary>
    public required int ListingCount { get; init; }
}
