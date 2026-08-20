namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>Renames or re-describes a category. The key never changes — it is the contract.</summary>
public sealed record UpdateGuideCategoryRequest
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? Icon { get; init; }

    public int Order { get; init; }

    public string? ParentKey { get; init; }
}
