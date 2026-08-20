using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Location.Domain.Experience;

/// <summary>Identifies a <see cref="CountryExpertise"/>.</summary>
public sealed record CountryExpertiseId(Guid Value) : StronglyTypedId(Value)
{
    public static CountryExpertiseId New() => new(Guid.NewGuid());

    public static CountryExpertiseId From(Guid value) => new(value);
}
