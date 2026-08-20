using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Identifiers;

/// <summary>Identifies a hashtag. One row per normalised slug, platform-wide.</summary>
public sealed record HashtagId(Guid Value) : StronglyTypedId(Value)
{
    public static HashtagId New() => new(Guid.NewGuid());

    public static HashtagId From(Guid value) => new(value);
}
