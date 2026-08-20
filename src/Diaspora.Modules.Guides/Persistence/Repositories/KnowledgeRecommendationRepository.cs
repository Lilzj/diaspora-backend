using Diaspora.Modules.Guides.Domain.Recommendations;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Persistence.Repositories;

/// <summary>
/// EF Core repository for curated next-read sets.
/// <para>
/// The source guide is compared as a <c>GuideId</c>, not as a <c>Guid</c>. EF translates equality
/// over a value-converted property; reaching through to <c>.Value</c> makes the whole query
/// untranslatable — a run-time failure the compiler cannot see, and the rule B4 recorded and B10
/// paid for in five repositories.
/// </para>
/// </summary>
public sealed class KnowledgeRecommendationRepository : IKnowledgeRecommendationRepository
{
    private readonly DiasporaDbContext _dbContext;

    public KnowledgeRecommendationRepository(DiasporaDbContext dbContext) =>
        _dbContext = dbContext;

    private DbSet<KnowledgeRecommendation> Recommendations =>
        _dbContext.Set<KnowledgeRecommendation>();

    public async Task<KnowledgeRecommendation?> GetBySourceAsync(
        GuideId sourceGuideId, CancellationToken cancellationToken = default) =>
        await Recommendations
            .Include(recommendation => recommendation.Reads)
            .FirstOrDefaultAsync(
                recommendation => recommendation.SourceGuideId == sourceGuideId, cancellationToken);

    public async Task<KnowledgeRecommendation?> GetBySourceForReadAsync(
        GuideId sourceGuideId, CancellationToken cancellationToken = default) =>
        await Recommendations
            .AsNoTracking()
            .Include(recommendation => recommendation.Reads)
            .FirstOrDefaultAsync(
                recommendation => recommendation.SourceGuideId == sourceGuideId, cancellationToken);

    public async Task AddAsync(
        KnowledgeRecommendation recommendation, CancellationToken cancellationToken = default) =>
        await Recommendations.AddAsync(recommendation, cancellationToken);
}
