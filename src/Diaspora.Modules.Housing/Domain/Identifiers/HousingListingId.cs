using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.Identifiers;

/// <summary>An accommodation listing's identity.</summary>
public sealed record HousingListingId(Guid Value) : StronglyTypedId(Value)
{
    public static HousingListingId New() => new(Guid.NewGuid());

    public static HousingListingId From(Guid value) => new(value);
}
