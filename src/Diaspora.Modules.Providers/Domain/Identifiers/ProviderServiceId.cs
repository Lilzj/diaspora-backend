using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Identifiers;

/// <summary>Identifies one service a provider offers.</summary>
public sealed record ProviderServiceId(Guid Value) : StronglyTypedId(Value)
{
    public static ProviderServiceId New() => new(Guid.NewGuid());

    public static ProviderServiceId From(Guid value) => new(value);
}
