using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Identifiers;

/// <summary>Identifies one curated onboarding sequence through a country's knowledge.</summary>
public sealed record LearningPathId(Guid Value) : StronglyTypedId(Value)
{
    public static LearningPathId New() => new(Guid.NewGuid());

    public static LearningPathId From(Guid value) => new(value);
}
