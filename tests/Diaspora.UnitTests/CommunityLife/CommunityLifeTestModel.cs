using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Diaspora.UnitTests.CommunityLife;

/// <summary>
/// A distinct model cache key for the Community Life test hosts.
/// <para>
/// <b>EF caches one model per <c>DbContext</c> CLR type, and the unit suite builds
/// <c>DiasporaDbContext</c> with several different contributor sets.</b> Without a distinct key,
/// whichever test class happens to run first decides what every other one is looking at — so the
/// Community Life hosts and the soft-delete host share a model, one of them wins, and the other
/// fails with "the entity type … was not found".
/// </para>
/// <para>
/// <b>This is not hypothetical: adding B12.1's two persistence test classes broke
/// <c>SoftDeleteCascadeTests</c>, which had passed since B1.0.1.</b> Neither class had a defect —
/// they were looking at each other's model. The failure only appears when the whole suite runs, so
/// running one class in isolation says everything is fine.
/// </para>
/// <para>
/// The architecture suite solved the same problem the same way for <c>PlatformModel</c>; this is
/// that fix, applied where the unit suite needed it.
/// </para>
/// </summary>
internal sealed class CommunityLifeModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime) =>
        (typeof(CommunityLifeModelCacheKeyFactory), designTime);
}
