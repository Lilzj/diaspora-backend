using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Identifiers;

/// <summary>One campaign's delivery on one day, on one surface.</summary>
public sealed record AdPerformanceDayId(Guid Value) : StronglyTypedId(Value)
{
    public static AdPerformanceDayId New() => new(Guid.NewGuid());

    public static AdPerformanceDayId From(Guid value) => new(value);
}
