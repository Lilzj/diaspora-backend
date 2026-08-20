using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Identifiers;

/// <summary>Identifies a provider's descriptive profile block.</summary>
public sealed record ProviderProfileId(Guid Value) : StronglyTypedId(Value)
{
    public static ProviderProfileId New() => new(Guid.NewGuid());

    public static ProviderProfileId From(Guid value) => new(value);
}
