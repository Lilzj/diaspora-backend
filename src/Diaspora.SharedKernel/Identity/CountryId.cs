using Diaspora.SharedKernel.Primitives;

namespace Diaspora.SharedKernel.Identity;

public sealed record CountryId(Guid Value) : StronglyTypedId(Value)
{
    public static CountryId New() => new(Guid.NewGuid());
    public static CountryId From(Guid value) => new(value);
}
