using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Identifiers;

/// <summary>Identifies one language a provider serves customers in.</summary>
public sealed record ProviderLanguageId(Guid Value) : StronglyTypedId(Value)
{
    public static ProviderLanguageId New() => new(Guid.NewGuid());

    public static ProviderLanguageId From(Guid value) => new(value);
}
