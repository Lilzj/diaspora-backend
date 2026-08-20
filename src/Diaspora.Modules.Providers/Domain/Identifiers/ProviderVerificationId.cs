using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Identifiers;

/// <summary>Identifies one verification claim a provider holds, projected from Trust.</summary>
public sealed record ProviderVerificationId(Guid Value) : StronglyTypedId(Value)
{
    public static ProviderVerificationId New() => new(Guid.NewGuid());

    public static ProviderVerificationId From(Guid value) => new(value);
}
