using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Identifiers;

/// <summary>Identifies a dated override of the weekly schedule: a holiday, a closure, special hours.</summary>
public sealed record AvailabilityExceptionId(Guid Value) : StronglyTypedId(Value)
{
    public static AvailabilityExceptionId New() => new(Guid.NewGuid());

    public static AvailabilityExceptionId From(Guid value) => new(value);
}
