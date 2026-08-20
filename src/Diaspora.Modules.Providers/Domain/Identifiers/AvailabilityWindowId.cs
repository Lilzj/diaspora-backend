using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Identifiers;

/// <summary>Identifies one open period within a day. A day may hold several — a split shift is two.</summary>
public sealed record AvailabilityWindowId(Guid Value) : StronglyTypedId(Value)
{
    public static AvailabilityWindowId New() => new(Guid.NewGuid());

    public static AvailabilityWindowId From(Guid value) => new(value);
}
