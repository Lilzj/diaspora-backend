using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Location.Domain.Profiles;

/// <summary>Identifies a <see cref="LocationProfile"/>.</summary>
public sealed record LocationProfileId(Guid Value) : StronglyTypedId(Value)
{
    public static LocationProfileId New() => new(Guid.NewGuid());

    public static LocationProfileId From(Guid value) => new(value);
}
