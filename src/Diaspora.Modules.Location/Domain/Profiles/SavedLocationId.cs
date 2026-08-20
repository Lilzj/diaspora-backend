using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Location.Domain.Profiles;

/// <summary>Identifies a <see cref="SavedLocation"/>.</summary>
public sealed record SavedLocationId(Guid Value) : StronglyTypedId(Value)
{
    public static SavedLocationId New() => new(Guid.NewGuid());

    public static SavedLocationId From(Guid value) => new(value);
}
