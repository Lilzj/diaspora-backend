using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Identifiers;

/// <summary>Identifies one free-text tag on a provider.</summary>
public sealed record ProviderTagId(Guid Value) : StronglyTypedId(Value)
{
    public static ProviderTagId New() => new(Guid.NewGuid());

    public static ProviderTagId From(Guid value) => new(value);
}
