using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.Identifiers;

/// <summary>A neighbourhood's living profile. Distinct from B4's AreaId, which identifies the place itself.</summary>
public sealed record NeighborhoodProfileId(Guid Value) : StronglyTypedId(Value)
{
    public static NeighborhoodProfileId New() => new(Guid.NewGuid());

    public static NeighborhoodProfileId From(Guid value) => new(value);
}
