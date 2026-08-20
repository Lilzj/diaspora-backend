using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Identifiers;

/// <summary>Identifies a guide — the living document the whole module is arranged around.</summary>
public sealed record GuideId(Guid Value) : StronglyTypedId(Value)
{
    public static GuideId New() => new(Guid.NewGuid());

    public static GuideId From(Guid value) => new(value);
}
