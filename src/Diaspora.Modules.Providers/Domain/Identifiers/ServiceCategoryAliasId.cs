using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Identifiers;

/// <summary>Identifies an alternative name a category also answers to.</summary>
public sealed record ServiceCategoryAliasId(Guid Value) : StronglyTypedId(Value)
{
    public static ServiceCategoryAliasId New() => new(Guid.NewGuid());

    public static ServiceCategoryAliasId From(Guid value) => new(value);
}
