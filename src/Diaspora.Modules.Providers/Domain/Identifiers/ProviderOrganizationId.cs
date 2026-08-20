using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Identifiers;

/// <summary>Identifies the organizational detail of a provider that is a legal or community body.</summary>
public sealed record ProviderOrganizationId(Guid Value) : StronglyTypedId(Value)
{
    public static ProviderOrganizationId New() => new(Guid.NewGuid());

    public static ProviderOrganizationId From(Guid value) => new(value);
}
