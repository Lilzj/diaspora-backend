using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Identifiers;

/// <summary>Identifies one member's single reaction to one piece of content.</summary>
public sealed record ReactionId(Guid Value) : StronglyTypedId(Value)
{
    public static ReactionId New() => new(Guid.NewGuid());

    public static ReactionId From(Guid value) => new(value);
}
