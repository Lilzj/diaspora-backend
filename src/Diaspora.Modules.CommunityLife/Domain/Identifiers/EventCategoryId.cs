using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Identifiers;

/// <summary>A row in the configurable event-category catalogue.</summary>
public sealed record EventCategoryId(Guid Value) : StronglyTypedId(Value)
{
    public static EventCategoryId New() => new(Guid.NewGuid());

    public static EventCategoryId From(Guid value) => new(value);
}
