using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.Modules.Marketplace.Domain.Attributes;
using Diaspora.Modules.Marketplace.Domain.Catalog;

namespace Diaspora.Modules.Marketplace.Application.Mapping;

/// <summary>
/// Turns catalogue aggregates into wire responses.
/// <para>
/// Categories go out flat with their materialised paths rather than nested, so the client
/// assembles whatever shape its screen wants — a tree, a breadcrumb, a flat picker — from one
/// response. A nested payload would force a recursive renderer on every client for a structure
/// three levels deep.
/// </para>
/// </summary>
internal static class CatalogMapper
{
    public static ListingCategoryResponse ToResponse(ListingCategory category)
    {
        ArgumentNullException.ThrowIfNull(category);

        return new ListingCategoryResponse
        {
            CategoryId = category.Id.Value,
            Key = category.Key,
            Name = category.Name,
            Description = category.Description,
            ParentKey = category.ParentKey,
            Path = category.Path,
            Depth = category.Depth,
            IconKey = category.IconKey,
            Order = category.Order,
            IsActive = category.IsActive,
            ListingCount = category.ListingCount
        };
    }

    public static ListingAttributeDefinitionResponse ToResponse(
        ListingAttributeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return new ListingAttributeDefinitionResponse
        {
            DefinitionId = definition.Id.Value,
            Key = definition.Key,
            Label = definition.Label,
            CategoryPath = definition.CategoryPath,
            ValueKind = definition.ValueKind.ToString(),
            IsRequired = definition.IsRequired,
            IsFilterable = definition.IsFilterable,
            Unit = definition.Unit,
            Order = definition.Order,
            Choices =
            [
                .. definition.Choices.Select(choice => new AttributeChoiceResponse
                {
                    Value = choice.Value,
                    Label = choice.Label
                })
            ]
        };
    }
}
