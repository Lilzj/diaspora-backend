using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Identifiers;

/// <summary>Identifies a node in the hierarchical service category catalogue.</summary>
public sealed record ServiceCategoryId(Guid Value) : StronglyTypedId(Value)
{
    public static ServiceCategoryId New() => new(Guid.NewGuid());

    public static ServiceCategoryId From(Guid value) => new(value);
}
