using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Identifiers;

/// <summary>Identifies a professional credential a provider claims.</summary>
public sealed record ProviderCertificationId(Guid Value) : StronglyTypedId(Value)
{
    public static ProviderCertificationId New() => new(Guid.NewGuid());

    public static ProviderCertificationId From(Guid value) => new(value);
}
