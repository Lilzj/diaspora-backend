using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Identifiers;

/// <summary>Identifies a catalogue category guides are browsed under.</summary>
public sealed record GuideCategoryId(Guid Value) : StronglyTypedId(Value)
{
    public static GuideCategoryId New() => new(Guid.NewGuid());

    public static GuideCategoryId From(Guid value) => new(value);
}
