using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Identifiers;

/// <summary>Identifies one mention of one target inside one piece of content.</summary>
public sealed record MentionId(Guid Value) : StronglyTypedId(Value)
{
    public static MentionId New() => new(Guid.NewGuid());

    public static MentionId From(Guid value) => new(value);
}
