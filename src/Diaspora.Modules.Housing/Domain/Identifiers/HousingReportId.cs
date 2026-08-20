using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.Identifiers;

/// <summary>A report filed against a listing or a landlord.</summary>
public sealed record HousingReportId(Guid Value) : StronglyTypedId(Value)
{
    public static HousingReportId New() => new(Guid.NewGuid());

    public static HousingReportId From(Guid value) => new(value);
}
