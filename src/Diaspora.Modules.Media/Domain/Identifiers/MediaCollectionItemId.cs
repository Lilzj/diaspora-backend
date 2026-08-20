using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Media.Domain.Identifiers;

/// <summary>Identifies one placed asset inside a collection.</summary>
public sealed record MediaCollectionItemId(Guid Value) : StronglyTypedId(Value)
{
    public static MediaCollectionItemId New() => new(Guid.NewGuid());

    public static MediaCollectionItemId From(Guid value) => new(value);
}
