using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.Identifiers;

/// <summary>A scheduled visit to a property.</summary>
public sealed record HousingViewingId(Guid Value) : StronglyTypedId(Value)
{
    public static HousingViewingId New() => new(Guid.NewGuid());

    public static HousingViewingId From(Guid value) => new(value);
}
