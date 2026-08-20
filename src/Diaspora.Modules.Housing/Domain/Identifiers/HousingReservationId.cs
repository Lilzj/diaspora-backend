using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.Identifiers;

/// <summary>A hold on accommodation for a named member.</summary>
public sealed record HousingReservationId(Guid Value) : StronglyTypedId(Value)
{
    public static HousingReservationId New() => new(Guid.NewGuid());

    public static HousingReservationId From(Guid value) => new(value);
}
