namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>One node of the guide catalogue, as the category picker and filter row render it.</summary>
public sealed record GuideCategoryResponse
{
    public required Guid CategoryId { get; init; }

    /// <summary>The stable key guides store and URLs carry.</summary>
    public required string Key { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>An icon name the client resolves. Opaque to the server.</summary>
    public string? Icon { get; init; }

    public required int Order { get; init; }

    /// <summary>Whether new guides may choose it. Existing guides keep it either way.</summary>
    public required bool IsActive { get; init; }

    public string? ParentKey { get; init; }
}
