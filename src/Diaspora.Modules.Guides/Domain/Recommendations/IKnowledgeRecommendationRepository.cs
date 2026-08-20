using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Domain.Recommendations;

/// <summary>Reads and writes curated next-read sets.</summary>
public interface IKnowledgeRecommendationRepository
{
    /// <summary>The set for one guide, tracked. For commands.</summary>
    Task<KnowledgeRecommendation?> GetBySourceAsync(
        GuideId sourceGuideId, CancellationToken cancellationToken = default);

    /// <summary>The set for one guide, untracked. For the "what next" panel.</summary>
    Task<KnowledgeRecommendation?> GetBySourceForReadAsync(
        GuideId sourceGuideId, CancellationToken cancellationToken = default);

    Task AddAsync(
        KnowledgeRecommendation recommendation, CancellationToken cancellationToken = default);
}
