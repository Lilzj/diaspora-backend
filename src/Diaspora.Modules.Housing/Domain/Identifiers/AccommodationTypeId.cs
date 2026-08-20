using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.Identifiers;

/// <summary>One entry in the configurable accommodation-type catalogue.</summary>
public sealed record AccommodationTypeId(Guid Value) : StronglyTypedId(Value)
{
    public static AccommodationTypeId New() => new(Guid.NewGuid());

    public static AccommodationTypeId From(Guid value) => new(value);
}
