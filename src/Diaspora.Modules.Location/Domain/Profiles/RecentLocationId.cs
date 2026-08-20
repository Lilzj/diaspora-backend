using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Location.Domain.Profiles;

/// <summary>Identifies a <see cref="RecentLocation"/>.</summary>
public sealed record RecentLocationId(Guid Value) : StronglyTypedId(Value)
{
    public static RecentLocationId New() => new(Guid.NewGuid());

    public static RecentLocationId From(Guid value) => new(value);
}
