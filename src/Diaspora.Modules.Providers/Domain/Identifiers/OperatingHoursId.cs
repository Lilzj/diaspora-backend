using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Identifiers;

/// <summary>Identifies a provider's schedule for one day of the week.</summary>
public sealed record OperatingHoursId(Guid Value) : StronglyTypedId(Value)
{
    public static OperatingHoursId New() => new(Guid.NewGuid());

    public static OperatingHoursId From(Guid value) => new(value);
}
