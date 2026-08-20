using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Identifiers;

/// <summary>Identifies a reaction <em>kind</em> — the catalogue entry, not an instance of it.</summary>
public sealed record ReactionDefinitionId(Guid Value) : StronglyTypedId(Value)
{
    public static ReactionDefinitionId New() => new(Guid.NewGuid());

    public static ReactionDefinitionId From(Guid value) => new(value);
}
