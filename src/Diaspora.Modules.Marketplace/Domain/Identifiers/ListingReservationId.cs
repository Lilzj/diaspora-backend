using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.Identifiers;

/// <summary>A reservation's identity.</summary>
public sealed record ListingReservationId(Guid Value) : StronglyTypedId(Value)
{
    public static ListingReservationId New() => new(Guid.NewGuid());

    public static ListingReservationId From(Guid value) => new(value);
}
