using Diaspora.Modules.Providers.Application.Services;
using Diaspora.Modules.Providers.Contracts.V1.Responses;
using Diaspora.Modules.Providers.Domain.Reviews;

namespace Diaspora.Modules.Providers.Application.Mapping;

/// <summary>Turns recommendations into the shape the wire reads.</summary>
public static class ProviderRecommendationMapper
{
    public static ProviderRecommendationResponse ToResponse(
        ProviderRecommendation recommendation, string? recommenderName = null)
    {
        ArgumentNullException.ThrowIfNull(recommendation);

        return new ProviderRecommendationResponse
        {
            RecommendationId = recommendation.Id.Value,
            ProviderId = recommendation.ProviderId.Value,
            RecommenderId = recommendation.RecommenderId.Value,
            RecommenderName = recommenderName,
            Kind = ProviderWireNames.ToWire(recommendation.Kind),
            Note = recommendation.Note,
            Skill = recommendation.Skill,
            IsFeatured = recommendation.IsFeatured,
            CreatedAt = recommendation.CreatedAt
        };
    }
}
