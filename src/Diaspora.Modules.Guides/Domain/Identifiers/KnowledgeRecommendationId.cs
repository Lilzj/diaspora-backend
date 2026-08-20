using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Identifiers;

/// <summary>
/// Identifies the curated set of next reads that follows one guide.
/// <para>
/// One aggregate per source guide, so an editor arranging what comes after the getting-started
/// guide never loads or locks the guide itself — recommendations are editorial metadata about a
/// guide, and B8 owns the guide.
/// </para>
/// </summary>
public sealed record KnowledgeRecommendationId(Guid Value) : StronglyTypedId(Value)
{
    public static KnowledgeRecommendationId New() => new(Guid.NewGuid());

    public static KnowledgeRecommendationId From(Guid value) => new(value);
}
