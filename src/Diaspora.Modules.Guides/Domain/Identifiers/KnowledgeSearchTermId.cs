using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Identifiers;

/// <summary>Identifies one normalized search phrase, counted per country.</summary>
public sealed record KnowledgeSearchTermId(Guid Value) : StronglyTypedId(Value)
{
    public static KnowledgeSearchTermId New() => new(Guid.NewGuid());

    public static KnowledgeSearchTermId From(Guid value) => new(value);
}
