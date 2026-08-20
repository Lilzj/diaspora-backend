using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Media.Domain.Identifiers;

/// <summary>Identifies a <see cref="Collections.MediaCollection"/>.</summary>
public sealed record MediaCollectionId(Guid Value) : StronglyTypedId(Value)
{
    public static MediaCollectionId New() => new(Guid.NewGuid());

    public static MediaCollectionId From(Guid value) => new(value);
}
