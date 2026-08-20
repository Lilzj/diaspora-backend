using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Media.Domain.Identifiers;

/// <summary>Identifies one derived rendition of an asset.</summary>
public sealed record MediaVariantId(Guid Value) : StronglyTypedId(Value)
{
    public static MediaVariantId New() => new(Guid.NewGuid());

    public static MediaVariantId From(Guid value) => new(value);
}
