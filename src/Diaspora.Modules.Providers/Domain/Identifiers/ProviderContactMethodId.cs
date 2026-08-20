using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Identifiers;

/// <summary>Identifies one way to reach a provider: phone, WhatsApp, email, website, social link.</summary>
public sealed record ProviderContactMethodId(Guid Value) : StronglyTypedId(Value)
{
    public static ProviderContactMethodId New() => new(Guid.NewGuid());

    public static ProviderContactMethodId From(Guid value) => new(value);
}
