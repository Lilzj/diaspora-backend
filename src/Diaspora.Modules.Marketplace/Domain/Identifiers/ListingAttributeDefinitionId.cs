using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.Identifiers;

/// <summary>A category attribute definition's identity.</summary>
public sealed record ListingAttributeDefinitionId(Guid Value) : StronglyTypedId(Value)
{
    public static ListingAttributeDefinitionId New() => new(Guid.NewGuid());

    public static ListingAttributeDefinitionId From(Guid value) => new(value);
}
