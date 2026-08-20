using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Identifiers;

/// <summary>A purchased placement over somebody else's subject — a boost, a feature, a premium listing.</summary>
public sealed record PlacementId(Guid Value) : StronglyTypedId(Value)
{
    public static PlacementId New() => new(Guid.NewGuid());

    public static PlacementId From(Guid value) => new(value);
}
