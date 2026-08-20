using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.Identifiers;

/// <summary>One entry in the configurable amenity catalogue.</summary>
public sealed record HousingAmenityId(Guid Value) : StronglyTypedId(Value)
{
    public static HousingAmenityId New() => new(Guid.NewGuid());

    public static HousingAmenityId From(Guid value) => new(value);
}
