using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Identifiers;

/// <summary>Identifies one member's recommendation, endorsement or testimonial for a provider.</summary>
public sealed record ProviderRecommendationId(Guid Value) : StronglyTypedId(Value)
{
    public static ProviderRecommendationId New() => new(Guid.NewGuid());

    public static ProviderRecommendationId From(Guid value) => new(value);
}
