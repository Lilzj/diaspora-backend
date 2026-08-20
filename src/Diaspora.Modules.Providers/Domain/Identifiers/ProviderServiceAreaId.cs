using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Identifiers;

/// <summary>Identifies one area a provider will travel to or serve remotely.</summary>
public sealed record ProviderServiceAreaId(Guid Value) : StronglyTypedId(Value)
{
    public static ProviderServiceAreaId New() => new(Guid.NewGuid());

    public static ProviderServiceAreaId From(Guid value) => new(value);
}
