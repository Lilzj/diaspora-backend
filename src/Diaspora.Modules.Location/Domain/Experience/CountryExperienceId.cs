using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Location.Domain.Experience;

/// <summary>Identifies a <see cref="CountryExperience"/>.</summary>
public sealed record CountryExperienceId(Guid Value) : StronglyTypedId(Value)
{
    public static CountryExperienceId New() => new(Guid.NewGuid());

    public static CountryExperienceId From(Guid value) => new(value);
}
