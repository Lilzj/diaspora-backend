using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Identifiers;

/// <summary>Identifies a time-bounded packaging of a service: a promotion, a package, a seasonal menu.</summary>
public sealed record ServiceOfferingId(Guid Value) : StronglyTypedId(Value)
{
    public static ServiceOfferingId New() => new(Guid.NewGuid());

    public static ServiceOfferingId From(Guid value) => new(value);
}
