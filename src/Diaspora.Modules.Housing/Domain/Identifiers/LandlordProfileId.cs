using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.Identifiers;

/// <summary>A landlord, agency, hostel or employer letting accommodation.</summary>
public sealed record LandlordProfileId(Guid Value) : StronglyTypedId(Value)
{
    public static LandlordProfileId New() => new(Guid.NewGuid());

    public static LandlordProfileId From(Guid value) => new(value);
}
