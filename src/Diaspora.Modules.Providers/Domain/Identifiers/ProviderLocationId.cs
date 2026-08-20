using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Identifiers;

/// <summary>Identifies one physical place a provider operates from.</summary>
public sealed record ProviderLocationId(Guid Value) : StronglyTypedId(Value)
{
    public static ProviderLocationId New() => new(Guid.NewGuid());

    public static ProviderLocationId From(Guid value) => new(value);
}
