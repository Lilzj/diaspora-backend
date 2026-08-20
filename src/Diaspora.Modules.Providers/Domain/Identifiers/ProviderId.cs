using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Identifiers;

/// <summary>Identifies a provider — the aggregate root of the whole module.</summary>
public sealed record ProviderId(Guid Value) : StronglyTypedId(Value)
{
    public static ProviderId New() => new(Guid.NewGuid());

    public static ProviderId From(Guid value) => new(value);
}
