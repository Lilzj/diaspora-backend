using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Categories;

namespace Diaspora.Modules.Guides.Application.Mapping;

/// <summary>Turns a catalogue node into the wire shape the picker and filter row render.</summary>
public static class GuideCategoryMapper
{
    public static GuideCategoryResponse ToResponse(GuideCategory category)
    {
        ArgumentNullException.ThrowIfNull(category);

        return new GuideCategoryResponse
        {
            CategoryId = category.Id.Value,
            Key = category.Key,
            Name = category.Name,
            Description = category.Description,
            Icon = category.Icon,
            Order = category.Order,
            IsActive = category.IsActive,
            ParentKey = category.ParentKey
        };
    }
}
