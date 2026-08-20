using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Identifiers;

/// <summary>One currency the platform knows how to denominate, format and store.</summary>
public sealed record CurrencyDefinitionId(Guid Value) : StronglyTypedId(Value)
{
    public static CurrencyDefinitionId New() => new(Guid.NewGuid());

    public static CurrencyDefinitionId From(Guid value) => new(value);
}
