using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.Identifiers;

/// <summary>A listing's measured record.</summary>
public sealed record HousingAnalyticsId(Guid Value) : StronglyTypedId(Value)
{
    public static HousingAnalyticsId New() => new(Guid.NewGuid());

    public static HousingAnalyticsId From(Guid value) => new(value);
}
