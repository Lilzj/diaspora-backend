using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Identifiers;

/// <summary>Identifies one keyword on a service category, used by search and classification.</summary>
public sealed record ServiceCategoryKeywordId(Guid Value) : StronglyTypedId(Value)
{
    public static ServiceCategoryKeywordId New() => new(Guid.NewGuid());

    public static ServiceCategoryKeywordId From(Guid value) => new(value);
}
